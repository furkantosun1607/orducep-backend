using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrduCep.Application.Interfaces;
using OrduCep.Application.Services;
using OrduCep.Domain.Entities;

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

    /// <summary>
    /// Bir tesis (Örn: Berber) için o günkü tüm müsait (ve dolu) saatleri getirir.
    /// </summary>
    [HttpGet("slots/{facilityId}/{date}")]
    public async Task<IActionResult> GetAvailableTimeSlots(Guid facilityId, DateTime date)
    {
        try
        {
            var slots = await _reservationService.GetAvailableTimeSlotsAsync(facilityId, date);
            return Ok(slots);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Bir orduevi için bağlı tesisleri getirir.
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
                TemplateId = f.FacilityTemplateId,
                f.IsAppointmentBased,
                f.OpeningTime,
                f.ClosingTime,
                f.SlotDurationInMinutes
            })
            .ToListAsync();
        return Ok(facilities);
    }

    /// <summary>
    /// Admin: Bir orduevine yeni tesis (hizmet birimi) ekler.
    /// </summary>
    [HttpPost("facilities")]
    public async Task<IActionResult> CreateFacility([FromBody] CreateFacilityRequest request, [FromServices] IApplicationDbContext context)
    {
        if (request.OrdueviId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { Message = "Orduevi ve tesis adı zorunludur." });
        }

        var ordueviExists = await context.Orduevleri.AnyAsync(o => o.Id == request.OrdueviId);
        if (!ordueviExists)
        {
            return NotFound(new { Message = "Orduevi bulunamadı." });
        }

        // Varsayılan şablon: Randevulu ise Berber, değilse Kantin üzerinden gidebilir.
        // Şimdilik herhangi bir şablon zorunlu tutmayıp ilk mevcut şablonu kullanıyoruz.
        var template = await context.FacilityTemplates.FirstOrDefaultAsync();
        if (template == null)
        {
            return BadRequest(new { Message = "Önce en az bir tesis şablonu tanımlanmalıdır." });
        }

        static TimeSpan ParseTimeOrDefault(string? input, TimeSpan fallback)
        {
            if (string.IsNullOrWhiteSpace(input))
                return fallback;

            // "08:00" veya "08:00:00" gibi formatları kabul et
            return TimeSpan.TryParse(input.Trim(), out var parsed) ? parsed : fallback;
        }

        var facility = new Facility
        {
            Id = Guid.NewGuid(),
            OrdueviId = request.OrdueviId,
            FacilityTemplateId = template.Id,
            Name = request.Name.Trim(),
            IsAppointmentBased = request.IsAppointmentBased,
            OpeningTime = ParseTimeOrDefault(request.OpeningTime, new TimeSpan(8, 0, 0)),
            ClosingTime = ParseTimeOrDefault(request.ClosingTime, new TimeSpan(17, 0, 0)),
            SlotDurationInMinutes = request.IsAppointmentBased
                ? (request.SlotDurationInMinutes > 0 ? request.SlotDurationInMinutes : 15)
                : 0,
            IsActive = true
        };

        context.Facilities.Add(facility);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            facility.Id,
            facility.Name,
            TemplateId = facility.FacilityTemplateId,
            facility.IsAppointmentBased,
            facility.OpeningTime,
            facility.ClosingTime,
            facility.SlotDurationInMinutes
        });
    }

    /// <summary>
    /// Admin: Bir tesisi (hizmet birimini) tamamen siler.
    /// </summary>
    [HttpDelete("facilities/{facilityId:guid}")]
    public async Task<IActionResult> DeleteFacility(Guid facilityId, [FromServices] IApplicationDbContext context)
    {
        var facility = await context.Facilities.FirstOrDefaultAsync(f => f.Id == facilityId);
        if (facility == null)
        {
            return NotFound(new { Message = "Tesis bulunamadı." });
        }

        context.Facilities.Remove(facility);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    /// <summary>
    /// Kullanıcının randevuyu 5 dakika boyunca kilitlemesi (Çifte randevuyu engellemek için tıklandığı an çalışır).
    /// </summary>
    [HttpPost("lock")]
    public async Task<IActionResult> LockTimeSlot([FromBody] LockRequest request)
    {
        var success = await _reservationService.LockTimeSlotAsync(
            request.FacilityId, 
            request.StartTime, 
            request.EndTime,
            request.UserId);

        if (success)
            return Ok(new { Message = "Randevu sizin için 5 dakikalığına geçici olarak kilitlendi." });
        else
            return Conflict(new { Message = "Üzgünüz, bu saat dilimi az önce bir başka kullanıcı tarafından seçildi." });
    }

    /// <summary>
    /// Kilidi alınmış olan randevuyu son olarak onaylamak (Onayla tuşuna basıldığında).
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
}

// Request Nesneleri (İç içe kısa yazıyorum test için)
public class LockRequest
{
    public Guid FacilityId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string UserId { get; set; } = string.Empty;
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
    public bool IsAppointmentBased { get; set; } = true;
    public string? OpeningTime { get; set; }
    public string? ClosingTime { get; set; }
    public int SlotDurationInMinutes { get; set; } = 15;
}
