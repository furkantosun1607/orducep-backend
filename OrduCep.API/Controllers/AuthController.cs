using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrduCep.Application.Interfaces;
using OrduCep.Domain.Entities;

namespace OrduCep.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public AuthController(IApplicationDbContext context)
    {
        _context = context;
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
            PasswordHash = HashPassword(request.Password),
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
            user.IdentityNumber,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
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

        if (user is null || user.PasswordHash != HashPassword(request.Password))
            return Unauthorized(new { Message = "Kimlik numarası veya şifre hatalı." });

        var userId = user.Id.ToString();
        var managedFacilities = await _context.FacilityStaffs
            .Include(s => s.Facility)
            .ThenInclude(f => f.Orduevi)
            .Where(s => s.UserId == userId)
            .Select(s => new
            {
                StaffId = s.Id,
                s.FacilityId,
                FacilityName = s.Facility.Name,
                s.Facility.OrdueviId,
                OrdueviName = s.Facility.Orduevi.Name,
                Role = s.Role.ToString()
            })
            .ToListAsync();

        return Ok(new
        {
            user.Id,
            user.IdentityNumber,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.Relation,
            user.OwnerTcNo,
            user.OwnerFirstName,
            user.OwnerLastName,
            user.OwnerRank,
            ManagedFacilities = managedFacilities
        });
    }

    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
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
