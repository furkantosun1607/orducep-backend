using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrduCep.Application.Interfaces;
using OrduCep.Application.Services;
using OrduCep.Domain.Entities;
using OrduCep.Domain.Enums;

namespace OrduCep.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;

    public ReservationsController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    // ──────────────────────────────────────────────
    //  SLOT & RANDEVU İŞLEMLERİ
    // ──────────────────────────────────────────────

    /// <summary>
    /// Bir tesis için o günkü müsait (ve dolu) zaman dilimlerini getirir.
    /// </summary>
    [HttpGet("slots/{facilityId}/{date}")]
    public async Task<IActionResult> GetAvailableTimeSlots(Guid facilityId, DateTime date, [FromQuery] Guid? serviceId = null)
    {
        try
        {
            var slots = await _reservationService.GetAvailableTimeSlotsAsync(facilityId, date, serviceId);
            return Ok(slots);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Randevuyu 5 dakika boyunca kilitler. Kapasite kontrolü yapılır.
    /// </summary>
    [HttpPost("lock")]
    public async Task<IActionResult> LockTimeSlot([FromBody] LockRequest request)
    {
        var success = await _reservationService.LockTimeSlotAsync(
            request.FacilityId,
            request.StartTime,
            request.ServiceId,
            request.ResourceId,
            request.UserId,
            request.GuestCount);

        if (success)
            return Ok(new { Message = "Randevu sizin için 5 dakikalığına geçici olarak kilitlendi." });
        else
            return Conflict(new { Message = "Kapasite dolu veya bu zaman dilimi başka bir kullanıcı tarafından alınmış." });
    }

    /// <summary>
    /// Kilitlenmiş randevuyu onaylar.
    /// </summary>
    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmReservation([FromBody] ConfirmRequest request)
    {
        var success = await _reservationService.ConfirmReservationAsync(request.FacilityId, request.StartTime, request.UserId);

        if (success)
            return Ok(new { Message = "Randevunuz başarıyla oluşturuldu." });
        else
            return BadRequest(new { Message = "5 dakikalık süreniz dolmuş veya geçersiz bir işlem. Lütfen baştan alın." });
    }

    // ──────────────────────────────────────────────
    //  KULLANICI (ÜYE) İŞLEMLERİ
    // ──────────────────────────────────────────────

    /// <summary>
    /// Kullanıcının kendi geçmiş ve gelecek randevularını getirir.
    /// </summary>
    [HttpGet("my-reservations")]
    public async Task<IActionResult> GetMyReservations([FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { Message = "Kullanıcı ID (userId) zorunludur." });

        var reservations = await _reservationService.GetUserReservationsAsync(userId);
        return Ok(reservations);
    }

    /// <summary>
    /// Kullanıcının randevusunu iptal etmesini sağlar.
    /// </summary>
    [HttpPut("cancel/{id:guid}")]
    public async Task<IActionResult> CancelReservation(Guid id, [FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { Message = "Kullanıcı ID (userId) zorunludur." });

        var success = await _reservationService.CancelReservationAsync(id, userId);

        if (success)
            return Ok(new { Message = "Randevunuz başarıyla iptal edildi." });
        else
            return BadRequest(new { Message = "Randevu bulunamadı, geçmiş bir randevu veya iptal etmeye yetkiniz yok." });
    }

    /// <summary>
    /// Bir tesisin belirli bir tarih aralığındaki günlük müsaitlik takvimini getirir.
    /// </summary>
    [HttpGet("facilities/{facilityId:guid}/availability-calendar")]
    public async Task<IActionResult> GetFacilityAvailabilityCalendar(
        Guid facilityId, 
        [FromQuery] DateTime startDate, 
        [FromQuery] DateTime endDate, 
        [FromQuery] Guid? serviceId = null)
    {
        // Tarih verilmezse varsayılan olarak bugünden itibaren 30 gün gösterelim
        if (startDate == default) startDate = DateTime.UtcNow.Date;
        if (endDate == default) endDate = startDate.AddDays(30);

        try
        {
            var calendar = await _reservationService.GetFacilityAvailabilityCalendarAsync(facilityId, startDate, endDate, serviceId);
            return Ok(calendar);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    // ──────────────────────────────────────────────
    //  TESİS (FACILITY) YÖNETİMİ
    // ──────────────────────────────────────────────

    /// <summary>
    /// Bir orduevine ait tesisleri getirir.
    /// </summary>
    [HttpGet("facilities/{ordueviId:guid}")]
    public async Task<IActionResult> GetFacilities(Guid ordueviId, [FromServices] IApplicationDbContext context)
    {
        var facilities = await context.Facilities
            .Where(f => f.OrdueviId == ordueviId && f.IsActive)
            .Select(f => new
            {
                f.Id,
                f.Name,
                Category = f.Category.ToString(),
                AppointmentMode = f.AppointmentMode.ToString(),
                f.MaxConcurrency,
                f.BufferMinutes,
                f.DefaultSlotDurationMinutes,
                f.OpeningTime,
                f.ClosingTime,
                f.Description,
                f.Icon
            })
            .ToListAsync();

        return Ok(facilities);
    }

    /// <summary>
    /// Yeni tesis oluşturur (Admin). Kategori, randevu modu ve kapasite bilgileri gönderilir.
    /// </summary>
    [HttpPost("facilities")]
    public async Task<IActionResult> CreateFacility([FromBody] CreateFacilityRequest request, [FromServices] IApplicationDbContext context)
    {
        if (request.OrdueviId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { Message = "Orduevi ve tesis adı zorunludur." });

        var ordueviExists = await context.Orduevleri.AnyAsync(o => o.Id == request.OrdueviId);
        if (!ordueviExists)
            return NotFound(new { Message = "Orduevi bulunamadı." });

        if (!Enum.TryParse<FacilityCategory>(request.Category, true, out var category))
            category = FacilityCategory.TimeBased;

        if (!Enum.TryParse<AppointmentMode>(request.AppointmentMode, true, out var mode))
            mode = AppointmentMode.AppointmentOnly;

        static TimeSpan ParseTimeOrDefault(string? input, TimeSpan fallback)
        {
            if (string.IsNullOrWhiteSpace(input)) return fallback;
            return TimeSpan.TryParse(input.Trim(), out var parsed) ? parsed : fallback;
        }

        var facility = new Facility
        {
            Id = Guid.NewGuid(),
            OrdueviId = request.OrdueviId,
            Name = request.Name.Trim(),
            Category = category,
            AppointmentMode = mode,
            MaxConcurrency = request.MaxConcurrency > 0 ? request.MaxConcurrency : 1,
            BufferMinutes = request.BufferMinutes >= 0 ? request.BufferMinutes : 0,
            DefaultSlotDurationMinutes = request.DefaultSlotDurationMinutes > 0 ? request.DefaultSlotDurationMinutes : 30,
            OpeningTime = ParseTimeOrDefault(request.OpeningTime, new TimeSpan(8, 0, 0)),
            ClosingTime = ParseTimeOrDefault(request.ClosingTime, new TimeSpan(17, 0, 0)),
            Description = request.Description?.Trim() ?? string.Empty,
            Icon = request.Icon?.Trim() ?? "default-icon",
            IsActive = true
        };

        context.Facilities.Add(facility);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            facility.Id,
            facility.Name,
            Category = facility.Category.ToString(),
            AppointmentMode = facility.AppointmentMode.ToString(),
            facility.MaxConcurrency,
            facility.BufferMinutes,
            facility.DefaultSlotDurationMinutes,
            facility.OpeningTime,
            facility.ClosingTime
        });
    }

    /// <summary>
    /// Tesisi siler (Admin).
    /// </summary>
    [HttpDelete("facilities/{facilityId:guid}")]
    public async Task<IActionResult> DeleteFacility(Guid facilityId, [FromServices] IApplicationDbContext context)
    {
        var facility = await context.Facilities.FirstOrDefaultAsync(f => f.Id == facilityId);
        if (facility == null)
            return NotFound(new { Message = "Tesis bulunamadı." });

        context.Facilities.Remove(facility);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    // ──────────────────────────────────────────────
    //  KAYNAK (RESOURCE) YÖNETİMİ
    // ──────────────────────────────────────────────

    /// <summary>
    /// Bir tesise ait kaynakları (koltuk, masa vb.) getirir.
    /// </summary>
    [HttpGet("facilities/{facilityId:guid}/resources")]
    public async Task<IActionResult> GetResources(Guid facilityId, [FromServices] IApplicationDbContext context)
    {
        var resources = await context.Resources
            .Where(r => r.FacilityId == facilityId && r.IsActive)
            .Select(r => new
            {
                r.Id,
                r.Name,
                Type = r.Type.ToString(),
                r.Capacity,
                r.Tags
            })
            .ToListAsync();

        return Ok(resources);
    }

    /// <summary>
    /// Tesise yeni kaynak (koltuk / masa / oda) ekler.
    /// </summary>
    [HttpPost("facilities/{facilityId:guid}/resources")]
    public async Task<IActionResult> CreateResource(Guid facilityId, [FromBody] CreateResourceRequest request, [FromServices] IApplicationDbContext context)
    {
        var facilityExists = await context.Facilities.AnyAsync(f => f.Id == facilityId);
        if (!facilityExists)
            return NotFound(new { Message = "Tesis bulunamadı." });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { Message = "Kaynak adı zorunludur." });

        if (!Enum.TryParse<ResourceType>(request.Type, true, out var resourceType))
            resourceType = ResourceType.Generic;

        var resource = new Resource
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            Name = request.Name.Trim(),
            Type = resourceType,
            Capacity = request.Capacity > 0 ? request.Capacity : 1,
            IsActive = true,
            Tags = request.Tags?.Trim() ?? string.Empty
        };

        context.Resources.Add(resource);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            resource.Id,
            resource.Name,
            Type = resource.Type.ToString(),
            resource.Capacity,
            resource.Tags
        });
    }

    /// <summary>
    /// Kaynağı siler.
    /// </summary>
    [HttpDelete("facilities/{facilityId:guid}/resources/{resourceId:guid}")]
    public async Task<IActionResult> DeleteResource(Guid facilityId, Guid resourceId, [FromServices] IApplicationDbContext context)
    {
        var resource = await context.Resources
            .FirstOrDefaultAsync(r => r.Id == resourceId && r.FacilityId == facilityId);

        if (resource == null)
            return NotFound(new { Message = "Kaynak bulunamadı." });

        context.Resources.Remove(resource);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    // ──────────────────────────────────────────────
    //  HİZMET (SERVICE) YÖNETİMİ
    // ──────────────────────────────────────────────

    /// <summary>
    /// Bir tesise ait hizmetleri getirir.
    /// </summary>
    [HttpGet("facilities/{facilityId:guid}/services")]
    public async Task<IActionResult> GetServices(Guid facilityId, [FromServices] IApplicationDbContext context)
    {
        var services = await context.FacilityServices
            .Where(s => s.FacilityId == facilityId && s.IsActive)
            .Select(s => new
            {
                s.Id,
                s.ServiceName,
                s.Price,
                s.DurationMinutes,
                s.BufferMinutes
            })
            .ToListAsync();

        return Ok(services);
    }

    /// <summary>
    /// Tesise yeni hizmet ekler.
    /// </summary>
    [HttpPost("facilities/{facilityId:guid}/services")]
    public async Task<IActionResult> CreateService(Guid facilityId, [FromBody] CreateServiceRequest request, [FromServices] IApplicationDbContext context)
    {
        var facilityExists = await context.Facilities.AnyAsync(f => f.Id == facilityId);
        if (!facilityExists)
            return NotFound(new { Message = "Tesis bulunamadı." });

        if (string.IsNullOrWhiteSpace(request.ServiceName))
            return BadRequest(new { Message = "Hizmet adı zorunludur." });

        var service = new FacilityService
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            ServiceName = request.ServiceName.Trim(),
            Price = request.Price,
            DurationMinutes = request.DurationMinutes > 0 ? request.DurationMinutes : 30,
            BufferMinutes = request.BufferMinutes >= 0 ? request.BufferMinutes : 0,
            IsActive = true
        };

        context.FacilityServices.Add(service);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            service.Id,
            service.ServiceName,
            service.Price,
            service.DurationMinutes,
            service.BufferMinutes
        });
    }

    /// <summary>
    /// Hizmeti siler.
    /// </summary>
    [HttpDelete("facilities/{facilityId:guid}/services/{serviceId:guid}")]
    public async Task<IActionResult> DeleteService(Guid facilityId, Guid serviceId, [FromServices] IApplicationDbContext context)
    {
        var service = await context.FacilityServices
            .FirstOrDefaultAsync(s => s.Id == serviceId && s.FacilityId == facilityId);

        if (service == null)
            return NotFound(new { Message = "Hizmet bulunamadı." });

        context.FacilityServices.Remove(service);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }
}

// ──────────────────────────────────────────────
//  REQUEST MODELLERİ
// ──────────────────────────────────────────────

public class LockRequest
{
    public Guid FacilityId { get; set; }
    public DateTime StartTime { get; set; }
    public Guid? ServiceId { get; set; }
    public Guid? ResourceId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int GuestCount { get; set; } = 1;
}

public class ConfirmRequest
{
    public Guid FacilityId { get; set; }
    public DateTime StartTime { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class CreateFacilityRequest
{
    public Guid OrdueviId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>"TimeBased", "CapacityBased", "SpaceBased"</summary>
    public string Category { get; set; } = "TimeBased";
    /// <summary>"AppointmentOnly", "Mixed", "WalkInOnly"</summary>
    public string AppointmentMode { get; set; } = "AppointmentOnly";
    public int MaxConcurrency { get; set; } = 1;
    public int BufferMinutes { get; set; } = 0;
    public int DefaultSlotDurationMinutes { get; set; } = 30;
    public string? OpeningTime { get; set; }
    public string? ClosingTime { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
}

public class CreateResourceRequest
{
    public string Name { get; set; } = string.Empty;
    /// <summary>"Chair", "Table", "Room", "Staff", "Generic"</summary>
    public string Type { get; set; } = "Generic";
    public int Capacity { get; set; } = 1;
    public string? Tags { get; set; }
}

public class CreateServiceRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public int BufferMinutes { get; set; } = 0;
}
