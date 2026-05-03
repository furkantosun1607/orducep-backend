using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrduCep.Application.Interfaces;
using OrduCep.Domain.Entities;

namespace OrduCep.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/Admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public AdminUsersController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<AdminUsersResponse>> GetUsers([FromQuery] string? search = null)
    {
        var usersQuery = _context.MilitaryIdentityUsers.AsNoTracking();
        var trimmedSearch = search?.Trim();

        if (!string.IsNullOrWhiteSpace(trimmedSearch))
        {
            var q = trimmedSearch.ToLower();
            var digits = OnlyDigits(trimmedSearch);

            usersQuery = usersQuery.Where(u =>
                u.FirstName.ToLower().Contains(q) ||
                u.LastName.ToLower().Contains(q) ||
                u.PhoneNumber.Contains(trimmedSearch) ||
                u.OwnerRank.ToLower().Contains(q) ||
                u.Relation.ToLower().Contains(q) ||
                (digits != string.Empty && (
                    u.IdentityNumber.Contains(digits) ||
                    u.OwnerTcNo.Contains(digits)
                )));
        }

        var users = await usersQuery
            .OrderByDescending(u => u.CreatedAtUtc)
            .ThenBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(HttpContext.RequestAborted);

        var userIds = users.Select(u => u.Id.ToString()).ToList();
        var reservationCounts = await _context.Reservations.AsNoTracking()
            .Where(r => userIds.Contains(r.UserId))
            .GroupBy(r => r.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, HttpContext.RequestAborted);

        var staffCounts = await _context.FacilityStaffs.AsNoTracking()
            .Where(s => s.UserId != null && userIds.Contains(s.UserId))
            .GroupBy(s => s.UserId!)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, HttpContext.RequestAborted);

        var items = users
            .Select(user => ToDto(
                user,
                reservationCounts.GetValueOrDefault(user.Id.ToString()),
                staffCounts.GetValueOrDefault(user.Id.ToString())))
            .ToList();

        return Ok(new AdminUsersResponse
        {
            Total = items.Count,
            Users = items
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminUserDto>> UpdateUser(Guid id, [FromBody] AdminUserUpdateRequest request)
    {
        var user = await _context.MilitaryIdentityUsers
            .FirstOrDefaultAsync(u => u.Id == id, HttpContext.RequestAborted);

        if (user is null)
            return NotFound(new { Message = "Kullanıcı bulunamadı." });

        var identityNumber = OnlyDigits(request.IdentityNumber);
        if (identityNumber.Length != 11)
            return BadRequest(new { Message = "T.C. kimlik numarası 11 haneli olmalıdır." });

        var identityExists = await _context.MilitaryIdentityUsers
            .AnyAsync(u => u.Id != id && u.IdentityNumber == identityNumber, HttpContext.RequestAborted);

        if (identityExists)
            return Conflict(new { Message = "Bu T.C. kimlik numarası başka bir kullanıcıda kayıtlı." });

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return BadRequest(new { Message = "Ad ve soyad zorunludur." });

        var relation = string.IsNullOrWhiteSpace(request.Relation)
            ? "Kendisi"
            : request.Relation.Trim();
        var isRelative = !string.Equals(relation, "Kendisi", StringComparison.OrdinalIgnoreCase);

        if (isRelative)
        {
            var ownerIdentity = OnlyDigits(request.OwnerTcNo);
            if (ownerIdentity.Length != 11)
                return BadRequest(new { Message = "Asıl hak sahibinin T.C. kimlik numarası 11 haneli olmalıdır." });
            if (string.IsNullOrWhiteSpace(request.OwnerFirstName))
                return BadRequest(new { Message = "Asıl hak sahibinin adı zorunludur." });
            if (string.IsNullOrWhiteSpace(request.OwnerLastName))
                return BadRequest(new { Message = "Asıl hak sahibinin soyadı zorunludur." });

            user.OwnerTcNo = ownerIdentity;
            user.OwnerFirstName = request.OwnerFirstName.Trim();
            user.OwnerLastName = request.OwnerLastName.Trim();
        }
        else
        {
            user.OwnerTcNo = string.Empty;
            user.OwnerFirstName = string.Empty;
            user.OwnerLastName = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password.Length < 6)
                return BadRequest(new { Message = "Yeni şifre en az 6 karakter olmalıdır." });

            user.PasswordHash = PasswordHashing.Hash(request.Password);
        }

        user.IdentityNumber = identityNumber;
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty;
        user.Relation = relation;
        user.OwnerRank = request.OwnerRank?.Trim() ?? string.Empty;

        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        var userId = user.Id.ToString();
        var reservationCount = await _context.Reservations.AsNoTracking()
            .CountAsync(r => r.UserId == userId, HttpContext.RequestAborted);
        var staffCount = await _context.FacilityStaffs.AsNoTracking()
            .CountAsync(s => s.UserId == userId, HttpContext.RequestAborted);

        return Ok(ToDto(user, reservationCount, staffCount));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _context.MilitaryIdentityUsers
            .FirstOrDefaultAsync(u => u.Id == id, HttpContext.RequestAborted);

        if (user is null)
            return NotFound(new { Message = "Kullanıcı bulunamadı." });

        var userId = user.Id.ToString();

        var reservations = await _context.Reservations
            .Where(r => r.UserId == userId)
            .ToListAsync(HttpContext.RequestAborted);
        var staffAssignments = await _context.FacilityStaffs
            .Where(s => s.UserId == userId)
            .ToListAsync(HttpContext.RequestAborted);
        var voiceSessions = await _context.VoiceSessions
            .Where(s => s.UserId == userId)
            .ToListAsync(HttpContext.RequestAborted);

        _context.Reservations.RemoveRange(reservations);
        _context.FacilityStaffs.RemoveRange(staffAssignments);
        _context.VoiceSessions.RemoveRange(voiceSessions);
        _context.MilitaryIdentityUsers.Remove(user);

        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            Message = "Kullanıcı silindi.",
            DeletedReservations = reservations.Count,
            DeletedStaffAssignments = staffAssignments.Count,
            DeletedVoiceSessions = voiceSessions.Count
        });
    }

    private static AdminUserDto ToDto(MilitaryIdentityUser user, int reservationCount, int managedFacilityCount)
    {
        return new AdminUserDto
        {
            Id = user.Id,
            IdentityNumber = user.IdentityNumber,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            Relation = string.IsNullOrWhiteSpace(user.Relation) ? "Kendisi" : user.Relation,
            OwnerTcNo = user.OwnerTcNo,
            OwnerFirstName = user.OwnerFirstName,
            OwnerLastName = user.OwnerLastName,
            OwnerRank = user.OwnerRank,
            CanUseFacilities = PersonnelAccessRules.CanUseFacilities(user.OwnerRank),
            ReservationCount = reservationCount,
            ManagedFacilityCount = managedFacilityCount,
            CreatedAtUtc = user.CreatedAtUtc
        };
    }

    private static string OnlyDigits(string? value)
    {
        return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
    }
}

public class AdminUsersResponse
{
    public int Total { get; set; }
    public List<AdminUserDto> Users { get; set; } = new();
}

public class AdminUserDto
{
    public Guid Id { get; set; }
    public string IdentityNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Relation { get; set; } = "Kendisi";
    public string OwnerTcNo { get; set; } = string.Empty;
    public string OwnerFirstName { get; set; } = string.Empty;
    public string OwnerLastName { get; set; } = string.Empty;
    public string OwnerRank { get; set; } = string.Empty;
    public bool CanUseFacilities { get; set; }
    public int ReservationCount { get; set; }
    public int ManagedFacilityCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class AdminUserUpdateRequest
{
    public string IdentityNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Relation { get; set; } = "Kendisi";
    public string OwnerTcNo { get; set; } = string.Empty;
    public string OwnerFirstName { get; set; } = string.Empty;
    public string OwnerLastName { get; set; } = string.Empty;
    public string OwnerRank { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
