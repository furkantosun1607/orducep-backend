using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrduCep.API;
using OrduCep.API.Auth;
using OrduCep.Application.Interfaces;
using OrduCep.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

namespace OrduCep.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(IApplicationDbContext context, IConfiguration configuration, IJwtTokenService jwtTokenService)
    {
        _context = context;
        _configuration = configuration;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdentityNumber) || request.IdentityNumber.Length != 11)
            return BadRequest(new { Message = "T.C. kimlik numarası 11 haneli olmalıdır." });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return BadRequest(new { Message = "Şifre en az 6 karakter olmalıdır." });

        var exists = await _context.MilitaryIdentityUsers
            .AnyAsync(u => u.IdentityNumber == request.IdentityNumber);

        if (exists)
            return Conflict(new { Message = "Bu T.C. kimlik numarası ile kayıtlı kullanıcı zaten var." });

        // Yakını ise asıl hak sahibi bilgileri zorunlu
        var relation = request.Relation?.Trim() ?? "Kendisi";
        var isRelative = relation != "Kendisi";

        if (isRelative)
        {
            if (string.IsNullOrWhiteSpace(request.OwnerTcNo) || request.OwnerTcNo.Trim().Length != 11)
                return BadRequest(new { Message = "Asıl hak sahibinin T.C. kimlik numarası 11 haneli olmalıdır." });
            if (string.IsNullOrWhiteSpace(request.OwnerFirstName))
                return BadRequest(new { Message = "Asıl hak sahibinin adı zorunludur." });
            if (string.IsNullOrWhiteSpace(request.OwnerLastName))
                return BadRequest(new { Message = "Asıl hak sahibinin soyadı zorunludur." });
            if (string.IsNullOrWhiteSpace(request.OwnerRank))
                return BadRequest(new { Message = "Asıl hak sahibinin rütbesi zorunludur." });
        }

        var user = new MilitaryIdentityUser
        {
            Id = Guid.NewGuid(),
            IdentityNumber = request.IdentityNumber.Trim(),
            PasswordHash = PasswordHashing.Hash(request.Password),
            FirstName = request.FirstName?.Trim() ?? string.Empty,
            LastName = request.LastName?.Trim() ?? string.Empty,
            PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty,
            Relation = relation,
            OwnerTcNo = isRelative ? request.OwnerTcNo?.Trim() ?? string.Empty : string.Empty,
            OwnerFirstName = isRelative ? request.OwnerFirstName?.Trim() ?? string.Empty : string.Empty,
            OwnerLastName = isRelative ? request.OwnerLastName?.Trim() ?? string.Empty : string.Empty,
            OwnerRank = request.OwnerRank?.Trim() ?? string.Empty,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.MilitaryIdentityUsers.Add(user);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            user.Id,
            IdentityNumber = MaskIdentity(user.IdentityNumber),
            user.FirstName,
            user.LastName,
            PhoneNumber = MaskPhone(user.PhoneNumber),
            user.Relation,
            Message = "Kayıt başarıyla tamamlandı."
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdentityNumber) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { Message = "Kimlik numarası ve şifre zorunludur." });

        var user = await _context.MilitaryIdentityUsers
            .FirstOrDefaultAsync(u => u.IdentityNumber == request.IdentityNumber.Trim());

        if (user is null || !PasswordHashing.Verify(user.PasswordHash, request.Password, out var needsRehash))
            return Unauthorized(new { Message = "Kimlik numarası veya şifre hatalı." });

        if (needsRehash)
        {
            user.PasswordHash = PasswordHashing.Hash(request.Password);
            await _context.SaveChangesAsync(HttpContext.RequestAborted);
        }

        var userId = user.Id.ToString();
        var managedFacilityRows = await _context.FacilityStaffs
            .Include(s => s.Facility)
            .ThenInclude(f => f.Orduevi)
            .Where(s => s.UserId == userId)
            .ToListAsync();

        var managedFacilities = managedFacilityRows.Select(s => new
        {
            StaffId = s.Id,
            s.FacilityId,
            FacilityName = s.Facility.Name,
            s.Facility.OrdueviId,
            OrdueviName = s.Facility.Orduevi.Name,
            Role = PersonnelAccessRules.DisplayStaffRole(user.OwnerRank, s.Role)
        });

        var token = _jwtTokenService.CreateUserToken(user);

        return Ok(new
        {
            user.Id,
            IdentityNumber = MaskIdentity(user.IdentityNumber),
            user.FirstName,
            user.LastName,
            PhoneNumber = MaskPhone(user.PhoneNumber),
            user.Relation,
            OwnerTcNo = MaskIdentity(user.OwnerTcNo),
            user.OwnerFirstName,
            user.OwnerLastName,
            user.OwnerRank,
            CanUseFacilities = PersonnelAccessRules.CanUseFacilities(user.OwnerRank),
            ManagedFacilities = managedFacilities,
            Token = token.Token,
            token.ExpiresAtUtc
        });
    }

    [HttpPost("admin-login")]
    public IActionResult AdminLogin([FromBody] LoginRequest request)
    {
        var configuredIdentity = _configuration["ADMIN_IDENTITY_NUMBER"] ?? _configuration["ADMIN_USERNAME"];
        var configuredPassword = _configuration["ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(configuredIdentity) || string.IsNullOrWhiteSpace(configuredPassword))
            return StatusCode(503, new { Message = "Admin girişi için ADMIN_IDENTITY_NUMBER ve ADMIN_PASSWORD tanımlanmalıdır." });

        if (!FixedTimeEquals(configuredIdentity.Trim(), request.IdentityNumber?.Trim() ?? string.Empty) ||
            !FixedTimeEquals(configuredPassword, request.Password ?? string.Empty))
        {
            return Unauthorized(new { Message = "Admin kullanıcı adı veya şifre hatalı." });
        }

        var token = _jwtTokenService.CreateAdminToken(configuredIdentity.Trim());
        return Ok(new
        {
            Id = configuredIdentity.Trim(),
            FirstName = "Admin",
            LastName = string.Empty,
            CanUseFacilities = true,
            ManagedFacilities = Array.Empty<object>(),
            Token = token.Token,
            token.ExpiresAtUtc
        });
    }

    private static string MaskIdentity(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 11
            ? $"{digits[..3]}******{digits[^2..]}"
            : string.Empty;
    }

    private static string MaskPhone(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length < 4)
            return string.Empty;

        return $"*** *** {digits[^4..]}";
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}

public class RegisterRequest
{
    public string IdentityNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// "Kendisi" veya yakınlık: Eş, Çocuk, Anne, Baba vb.
    /// </summary>
    public string Relation { get; set; } = "Kendisi";

    // Asıl hak sahibi bilgileri (yakını ise zorunlu)
    public string OwnerTcNo { get; set; } = string.Empty;
    public string OwnerFirstName { get; set; } = string.Empty;
    public string OwnerLastName { get; set; } = string.Empty;
    public string OwnerRank { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string IdentityNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
