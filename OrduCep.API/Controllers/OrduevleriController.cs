using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrduCep.Application.Interfaces;
using OrduCep.Domain.Entities;
using OrduCep.Domain.Enums;

namespace OrduCep.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrduevleriController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public OrduevleriController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orduevleri = await _context.Orduevleri
            .Select(o => new
            {
                id = o.Id,
                name = o.Name,
                location = o.Address,
                description = o.Description,
                contactNumber = o.ContactNumber,
                address = o.Address,
                createdAt = o.CreatedAt,
                updatedAt = o.UpdatedAt
            })
            .ToListAsync();

        return Ok(orduevleri);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrdueviRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Address))
        {
            return BadRequest(new { Message = "İsim ve adres zorunludur." });
        }

        var orduevi = new Orduevi
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            ContactNumber = request.ContactNumber?.Trim() ?? string.Empty,
            AdminUserId = "admin-123",
            CreatedAt = DateTime.UtcNow
        };

        _context.Orduevleri.Add(orduevi);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            id = orduevi.Id,
            name = orduevi.Name,
            location = orduevi.Address,
            description = orduevi.Description,
            contactNumber = orduevi.ContactNumber,
            address = orduevi.Address,
            createdAt = orduevi.CreatedAt,
            updatedAt = orduevi.UpdatedAt
        });
    }

    /// <summary>
    /// Mevcut bir orduevinin bilgilerini günceller (Admin).
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrdueviRequest request)
    {
        var orduevi = await _context.Orduevleri.FirstOrDefaultAsync(o => o.Id == id);
        if (orduevi == null)
            return NotFound(new { Message = "Orduevi bulunamadı." });

        if (!string.IsNullOrWhiteSpace(request.Name))
            orduevi.Name = request.Name.Trim();

        if (request.Location != null)
            orduevi.Address = request.Location.Trim();

        if (request.Address != null)
            orduevi.Address = request.Address.Trim();

        if (request.Description != null)
            orduevi.Description = request.Description.Trim();

        if (request.ContactNumber != null)
            orduevi.ContactNumber = request.ContactNumber.Trim();

        orduevi.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            id = orduevi.Id,
            name = orduevi.Name,
            location = orduevi.Address,
            description = orduevi.Description,
            contactNumber = orduevi.ContactNumber,
            address = orduevi.Address,
            createdAt = orduevi.CreatedAt,
            updatedAt = orduevi.UpdatedAt,
            message = "Orduevi bilgileri başarıyla güncellendi."
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var orduevi = await _context.Orduevleri
            .FirstOrDefaultAsync(o => o.Id == id);

        if (orduevi == null)
        {
            return NotFound(new { Message = "Orduevi bulunamadı." });
        }

        _context.Orduevleri.Remove(orduevi);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    // 2. Bir orduevinin hizmetlerinin (facility) appointmentmodunu çeken endpoint
    [HttpGet("{ordueviId:guid}/facilities/{facilityId:guid}/appointment-mode")]
    public async Task<IActionResult> GetFacilityAppointmentMode(Guid ordueviId, Guid facilityId)
    {
        var facility = await _context.Facilities.FirstOrDefaultAsync(f => f.OrdueviId == ordueviId && f.Id == facilityId);
        if (facility == null) return NotFound(new { Message = "Tesis bulunamadı." });

        return Ok(new { AppointmentMode = facility.AppointmentMode.ToString() });
    }

    // 3. Bir orduevine yeni facility eklenmesi
    [HttpPost("{ordueviId:guid}/facilities")]
    public async Task<IActionResult> CreateFacility(Guid ordueviId, [FromBody] OrdueviCreateFacilityRequest request)
    {
        var ordueviExists = await _context.Orduevleri.AnyAsync(o => o.Id == ordueviId);
        if (!ordueviExists) return NotFound(new { Message = "Orduevi bulunamadı." });

        var facility = new Facility
        {
            Id = Guid.NewGuid(),
            OrdueviId = ordueviId,
            Name = request.Name,
            AppointmentMode = request.AppointmentMode,
            MaxConcurrency = request.Concurrency,
            DefaultSlotDurationMinutes = request.DefaultSlotDurationMinutes,
            OpeningTime = request.OpeningTime,
            ClosingTime = request.ClosingTime,
            Description = request.Description ?? string.Empty,
            ClosedDays = request.ClosedTimes ?? string.Empty,
            IsActive = true
        };

        _context.Facilities.Add(facility);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(facility);
    }

    // 4. Bir orduevinin facilitysinin silinmesi
    [HttpDelete("{ordueviId:guid}/facilities/{facilityId:guid}")]
    public async Task<IActionResult> DeleteFacility(Guid ordueviId, Guid facilityId)
    {
        var facility = await _context.Facilities.FirstOrDefaultAsync(f => f.OrdueviId == ordueviId && f.Id == facilityId);
        if (facility == null) return NotFound(new { Message = "Tesis bulunamadı." });

        _context.Facilities.Remove(facility);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    // 5. Bir orduevinin facilitysinin hangi günler kapalı olduğunu gösteren endpoint
    [HttpGet("{ordueviId:guid}/facilities/{facilityId:guid}/closed-days")]
    public async Task<IActionResult> GetFacilityClosedDays(Guid ordueviId, Guid facilityId)
    {
        var facility = await _context.Facilities.FirstOrDefaultAsync(f => f.OrdueviId == ordueviId && f.Id == facilityId);
        if (facility == null) return NotFound(new { Message = "Tesis bulunamadı." });

        return Ok(new { ClosedDays = facility.ClosedDays });
    }

    // 6. Bir orduevinin facilitysinin active olup olmadığını belirten endpoint
    [HttpGet("{ordueviId:guid}/facilities/{facilityId:guid}/is-active")]
    public async Task<IActionResult> GetFacilityIsActive(Guid ordueviId, Guid facilityId)
    {
        var facility = await _context.Facilities.FirstOrDefaultAsync(f => f.OrdueviId == ordueviId && f.Id == facilityId);
        if (facility == null) return NotFound(new { Message = "Tesis bulunamadı." });

        return Ok(new { IsActive = facility.IsActive });
    }

    // 7. FacilityService ekleme endpointi
    [HttpPost("{ordueviId:guid}/facilities/{facilityId:guid}/services")]
    public async Task<IActionResult> CreateFacilityService(Guid ordueviId, Guid facilityId, [FromBody] OrdueviCreateFacilityServiceRequest request)
    {
        var facilityExists = await _context.Facilities.AnyAsync(f => f.OrdueviId == ordueviId && f.Id == facilityId);
        if (!facilityExists) return NotFound(new { Message = "Tesis bulunamadı." });

        var service = new FacilityService
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            ServiceName = request.ServiceName,
            Price = request.Price,
            DurationMinutes = request.DurationMinutes,
            IsActive = true
        };

        _context.FacilityServices.Add(service);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(service);
    }

    // 8. FacilityService silme endpointi
    [HttpDelete("{ordueviId:guid}/facilities/{facilityId:guid}/services/{serviceId:guid}")]
    public async Task<IActionResult> DeleteFacilityService(Guid ordueviId, Guid facilityId, Guid serviceId)
    {
        var facilityExists = await _context.Facilities.AnyAsync(f => f.OrdueviId == ordueviId && f.Id == facilityId);
        if (!facilityExists) return NotFound(new { Message = "Tesis bulunamadı." });

        var service = await _context.FacilityServices.FirstOrDefaultAsync(s => s.FacilityId == facilityId && s.Id == serviceId);
        if (service == null) return NotFound(new { Message = "Hizmet bulunamadı." });

        _context.FacilityServices.Remove(service);
        await _context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }
}

public class CreateOrdueviRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
}

public class UpdateOrdueviRequest
{
    public string? Name { get; set; }
    /// <summary>Şehir / bölge bilgisi (örn: "Kızılay, Ankara")</summary>
    public string? Location { get; set; }
    /// <summary>Açık adres (örn: "Atatürk Bulvarı No:1"). Location ile aynı DB alanını günceller.</summary>
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? ContactNumber { get; set; }
}

public class OrdueviCreateFacilityRequest
{
    public string Name { get; set; } = string.Empty;
    public AppointmentMode AppointmentMode { get; set; }
    public int Concurrency { get; set; }
    public int DefaultSlotDurationMinutes { get; set; }
    public TimeSpan OpeningTime { get; set; }
    public TimeSpan ClosingTime { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ClosedTimes { get; set; } = string.Empty;
}

public class OrdueviCreateFacilityServiceRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
}
