using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrduCep.API;
using OrduCep.Application.Interfaces;
using OrduCep.Application.Services;
using OrduCep.Domain.Entities;
using OrduCep.Domain.Enums;
using System.Security.Claims;

namespace OrduCep.API.Controllers;

[ApiController]
[Authorize]
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
    public async Task<IActionResult> GetAvailableTimeSlots(
        Guid facilityId,
        DateTime date,
        [FromQuery] Guid? serviceId = null,
        [FromQuery] Guid? resourceId = null)
    {
        try
        {
            var slots = await _reservationService.GetAvailableTimeSlotsAsync(facilityId, date, serviceId, resourceId);
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
    public async Task<IActionResult> LockTimeSlot(
        [FromBody] LockRequest request,
        [FromServices] IApplicationDbContext context)
    {
        var userId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { Message = "Giriş bilgisi bulunamadı." });

        var access = await EnsureReservableUserAsync(userId, context);
        if (!access.Allowed)
            return StatusCode(access.StatusCode, new { Message = access.Message });

        var success = await _reservationService.LockTimeSlotAsync(
            request.FacilityId,
            request.StartTime,
            request.ServiceId,
            request.ResourceId,
            userId,
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
    public async Task<IActionResult> ConfirmReservation(
        [FromBody] ConfirmRequest request,
        [FromServices] IApplicationDbContext context)
    {
        var userId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { Message = "Giriş bilgisi bulunamadı." });

        var access = await EnsureReservableUserAsync(userId, context);
        if (!access.Allowed)
            return StatusCode(access.StatusCode, new { Message = access.Message });

        var success = await _reservationService.ConfirmReservationAsync(request.FacilityId, request.StartTime, userId);

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
    public async Task<IActionResult> GetMyReservations()
    {
        var userId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { Message = "Giriş bilgisi bulunamadı." });

        var reservations = await _reservationService.GetUserReservationsAsync(userId);
        return Ok(reservations);
    }

    /// <summary>
    /// Kullanıcının randevusunu iptal etmesini sağlar.
    /// </summary>
    [HttpPut("cancel/{id:guid}")]
    public async Task<IActionResult> CancelReservation(Guid id)
    {
        var userId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { Message = "Giriş bilgisi bulunamadı." });

        var success = await _reservationService.CancelReservationAsync(id, userId);

        if (success)
            return Ok(new { Message = "Randevunuz başarıyla iptal edildi." });
        else
            return BadRequest(new { Message = "Randevu bulunamadı, geçmiş bir randevu veya iptal etmeye yetkiniz yok." });
    }

    /// <summary>
    /// Bir tesisin randevularını yönetim/personel ekranı için müşteri bilgileriyle listeler.
    /// </summary>
    [HttpGet("facilities/{facilityId:guid}/reservations")]
    public async Task<IActionResult> GetFacilityReservations(
        Guid facilityId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? resourceId,
        [FromQuery] bool includeCancelled,
        [FromServices] IApplicationDbContext context)
    {
        var userId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { Message = "Giriş bilgisi bulunamadı." });

        var facilityExists = await context.Facilities.AnyAsync(f => f.Id == facilityId);
        if (!facilityExists)
            return NotFound(new { Message = "Tesis bulunamadı." });

        var isSuperAdmin = IsAdmin();
        var isFacilityStaff = await context.FacilityStaffs.AnyAsync(s => s.FacilityId == facilityId && s.UserId == userId);

        if (!isSuperAdmin && !isFacilityStaff)
            return StatusCode(403, new { Message = "Bu tesisin randevularını görme yetkiniz yok." });

        var from = (startDate ?? DateTime.Today).Date;
        var toExclusive = (endDate ?? from.AddDays(14)).Date.AddDays(1);

        var query = context.Reservations
            .Include(r => r.Service)
            .Include(r => r.Resource)
            .Where(r => r.FacilityId == facilityId &&
                        r.StartTime >= from &&
                        r.StartTime < toExclusive &&
                        r.Status != ReservationStatus.Locked);

        if (!includeCancelled)
            query = query.Where(r => r.Status != ReservationStatus.Cancelled);

        if (resourceId.HasValue)
            query = query.Where(r => r.ResourceId == resourceId.Value);

        var reservations = await query
            .OrderBy(r => r.StartTime)
            .ToListAsync();

        var reservationUserIds = reservations
            .Select(r => r.UserId)
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse)
            .Distinct()
            .ToList();

        var users = await context.MilitaryIdentityUsers
            .Where(u => reservationUserIds.Contains(u.Id))
            .ToListAsync();

        var userMap = users.ToDictionary(u => u.Id.ToString(), StringComparer.OrdinalIgnoreCase);

        var result = reservations.Select(r =>
        {
            userMap.TryGetValue(r.UserId, out var user);
            var ownerName = user == null
                ? string.Empty
                : user.Relation == "Kendisi"
                    ? BuildFullName(user.FirstName, user.LastName)
                    : BuildFullName(user.OwnerFirstName, user.OwnerLastName);

            return new
            {
                r.Id,
                r.FacilityId,
                r.ServiceId,
                ServiceName = r.Service != null ? r.Service.ServiceName : string.Empty,
                r.ResourceId,
                ResourceName = r.Resource != null ? r.Resource.Name : string.Empty,
                r.StartTime,
                r.EndTime,
                Status = r.Status.ToString(),
                r.GuestCount,
                r.Note,
                UserId = MaskIdentifier(r.UserId),
                CustomerName = user != null ? BuildFullName(user.FirstName, user.LastName) : "Bilinmeyen kullanıcı",
                CustomerIdentityNumber = MaskIdentity(user?.IdentityNumber),
                CustomerPhoneNumber = MaskPhone(user?.PhoneNumber),
                Relation = user?.Relation ?? string.Empty,
                OwnerName = ownerName,
                OwnerIdentityNumber = MaskIdentity(user?.OwnerTcNo),
                OwnerRank = user?.OwnerRank ?? string.Empty,
                CanCancel = r.Status != ReservationStatus.Cancelled && !IsPastOrCurrentBusinessTime(r.StartTime)
            };
        });

        return Ok(result);
    }

    /// <summary>
    /// Süper admin veya tesis personeli/berber kaynağı, tesise ait tek bir randevuyu iptal eder.
    /// </summary>
    [HttpPut("facilities/{facilityId:guid}/reservations/{reservationId:guid}/cancel")]
    public async Task<IActionResult> CancelFacilityReservation(
        Guid facilityId,
        Guid reservationId,
        [FromBody] ManagerCancelReservationRequest request,
        [FromServices] IApplicationDbContext context)
    {
        request ??= new ManagerCancelReservationRequest();
        var userId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { Message = "Giriş bilgisi bulunamadı." });

        var reservation = await context.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.FacilityId == facilityId);

        if (reservation == null)
            return NotFound(new { Message = "Randevu bulunamadı." });

        var isFacilityStaff = await context.FacilityStaffs
            .AnyAsync(s => s.FacilityId == facilityId && s.UserId == userId);

        if (!IsAdmin() && !isFacilityStaff)
            return StatusCode(403, new { Message = "Bu randevuyu iptal etme yetkiniz yok." });

        if (IsPastOrCurrentBusinessTime(reservation.StartTime))
            return BadRequest(new { Message = "Geçmiş randevular iptal edilemez." });

        if (reservation.Status == ReservationStatus.Cancelled)
            return Ok(new { Message = "Randevu zaten iptal edilmiş." });

        reservation.Status = ReservationStatus.Cancelled;
        reservation.LockedUntil = null;

        if (!string.IsNullOrWhiteSpace(request.Reason))
            reservation.Note = string.IsNullOrWhiteSpace(reservation.Note)
                ? $"İptal nedeni: {request.Reason.Trim()}"
                : $"{reservation.Note}\nİptal nedeni: {request.Reason.Trim()}";

        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new { Message = "Randevu iptal edildi." });
    }

    /// <summary>
    /// Bir tesisin belirli bir tarih aralığındaki günlük müsaitlik takvimini getirir.
    /// </summary>
    [HttpGet("facilities/{facilityId:guid}/availability-calendar")]
    public async Task<IActionResult> GetFacilityAvailabilityCalendar(
        Guid facilityId, 
        [FromQuery] DateTime startDate, 
        [FromQuery] DateTime endDate, 
        [FromQuery] Guid? serviceId = null,
        [FromQuery] Guid? resourceId = null)
    {
        // Tarih verilmezse varsayılan olarak bugünden itibaren 30 gün gösterelim
        if (startDate == default) startDate = DateTime.UtcNow.Date;
        if (endDate == default) endDate = startDate.AddDays(30);

        try
        {
            var calendar = await _reservationService.GetFacilityAvailabilityCalendarAsync(facilityId, startDate, endDate, serviceId, resourceId);
            return Ok(calendar);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    private static string BuildFullName(params string[] parts)
    {
        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
    }

    private string? CurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Admin");
    }

    private async Task<bool> CanManageFacilityAsync(Guid facilityId, IApplicationDbContext context)
    {
        if (IsAdmin())
            return true;

        var userId = CurrentUserId();
        return !string.IsNullOrWhiteSpace(userId) &&
               await context.FacilityStaffs.AnyAsync(s => s.FacilityId == facilityId && s.UserId == userId);
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

    private static string MaskIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Length <= 8
            ? "***"
            : $"{value[..4]}***{value[^4..]}";
    }

    private static async Task<(bool Allowed, int StatusCode, string Message)> EnsureReservableUserAsync(
        string? userId,
        IApplicationDbContext context)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return (false, StatusCodes.Status400BadRequest, "Kullanıcı bilgisi zorunludur.");

        if (!Guid.TryParse(userId, out var userGuid))
            return (false, StatusCodes.Status400BadRequest, "Kullanıcı bilgisi geçersiz.");

        var user = await context.MilitaryIdentityUsers.FirstOrDefaultAsync(u => u.Id == userGuid);
        if (user == null)
            return (false, StatusCodes.Status404NotFound, "Kullanıcı bulunamadı.");

        if (!PersonnelAccessRules.CanUseFacilities(user.OwnerRank))
            return (false, StatusCodes.Status403Forbidden, "Bu hesap askeri tesislerden faydalanamaz; yalnızca sorumlu olduğu birimleri yönetebilir.");

        return (true, StatusCodes.Status200OK, string.Empty);
    }

    private static DateTime ToBusinessLocalTime(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value.ToLocalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    private static bool IsPastOrCurrentBusinessTime(DateTime startTime)
    {
        return ToBusinessLocalTime(startTime) <= DateTime.Now;
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
            .OrderBy(f => f.Name)
            .Select(f => new
            {
                f.Id,
                f.OrdueviId,
                f.Name,
                Category = f.Category.ToString(),
                AppointmentMode = f.AppointmentMode.ToString(),
                f.MaxConcurrency,
                f.BufferMinutes,
                f.DefaultSlotDurationMinutes,
                f.OpeningTime,
                f.ClosingTime,
                f.Description,
                f.Icon,
                f.Image,
                f.IsActive,
                f.ClosedDays
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
        if (!IsAdmin())
            return StatusCode(403, new { Message = "Bu işlem için admin yetkisi gerekir." });

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
        if (!IsAdmin())
            return StatusCode(403, new { Message = "Bu işlem için admin yetkisi gerekir." });

        var facility = await context.Facilities.FirstOrDefaultAsync(f => f.Id == facilityId);
        if (facility == null)
            return NotFound(new { Message = "Tesis bulunamadı." });

        context.Facilities.Remove(facility);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    /// <summary>
    /// Mevcut bir tesisin tüm bilgilerini günceller (Admin). 
    /// Personel ve hizmet listeleri UPSERT/orphan-removal mantığıyla yönetilir.
    /// </summary>
    [HttpPut("facilities/{id:guid}")]
    public async Task<IActionResult> UpdateFacility(
        Guid id,
        [FromBody] UpdateFacilityRequestDto request,
        [FromServices] IApplicationDbContext context)
    {
        if (!await CanManageFacilityAsync(id, context))
            return StatusCode(403, new { Message = "Bu tesisi düzenleme yetkiniz yok." });

        var facility = await context.Facilities
            .Include(f => f.Services)
            .Include(f => f.StaffMembers)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (facility == null)
            return NotFound(new { Message = "Tesis bulunamadı." });

        // ── Temel alan güncellemeleri ──
        if (!string.IsNullOrWhiteSpace(request.Name))
            facility.Name = request.Name.Trim();

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            if (Enum.TryParse<FacilityCategory>(request.Category, true, out var category))
                facility.Category = category;
        }

        if (!string.IsNullOrWhiteSpace(request.AppointmentMode))
        {
            if (Enum.TryParse<AppointmentMode>(request.AppointmentMode, true, out var mode))
                facility.AppointmentMode = mode;
        }

        if (request.MaxConcurrency.HasValue && request.MaxConcurrency.Value > 0)
            facility.MaxConcurrency = request.MaxConcurrency.Value;

        if (request.BufferMinutes.HasValue && request.BufferMinutes.Value >= 0)
            facility.BufferMinutes = request.BufferMinutes.Value;

        if (request.DefaultSlotDurationMinutes.HasValue && request.DefaultSlotDurationMinutes.Value > 0)
            facility.DefaultSlotDurationMinutes = request.DefaultSlotDurationMinutes.Value;

        if (!string.IsNullOrWhiteSpace(request.OpeningTime) &&
            TimeSpan.TryParse(request.OpeningTime.Trim(), out var opening))
            facility.OpeningTime = opening;

        if (!string.IsNullOrWhiteSpace(request.ClosingTime) &&
            TimeSpan.TryParse(request.ClosingTime.Trim(), out var closing))
            facility.ClosingTime = closing;

        if (request.Description != null)
            facility.Description = request.Description.Trim();

        if (request.Image != null)
            facility.Image = request.Image; // Base64 string olarak saklanir

        // ── Kapalı günler ──
        if (request.Hours != null)
        {
            facility.ClosedDays = request.Hours.ClosedDays != null
                ? string.Join(",", request.Hours.ClosedDays.Select(d => d.Trim()).Where(d => !string.IsNullOrEmpty(d)))
                : string.Empty;
        }

        // ── Hizmetler: UPSERT + orphan removal ──
        if (request.Services != null)
        {
            var incomingIds = request.Services
                .Where(s => !string.IsNullOrWhiteSpace(s.Id))
                .Select(s => Guid.Parse(s.Id!))
                .ToHashSet();

            // Orphan removal: DB'de var ama gelen listede olmayan hizmetleri sil
            var toRemove = facility.Services
                .Where(s => !incomingIds.Contains(s.Id))
                .ToList();
            context.FacilityServices.RemoveRange(toRemove);

            foreach (var serviceDto in request.Services)
            {
                if (!string.IsNullOrWhiteSpace(serviceDto.Id) &&
                    Guid.TryParse(serviceDto.Id, out var serviceGuid))
                {
                    // Güncelle (mevcut kayıt)
                    var existing = facility.Services.FirstOrDefault(s => s.Id == serviceGuid);
                    if (existing != null)
                    {
                        if (!string.IsNullOrWhiteSpace(serviceDto.ServiceName))
                            existing.ServiceName = serviceDto.ServiceName.Trim();
                        existing.Price = serviceDto.Price;
                    }
                }
                else
                {
                    // Yeni kayıt ekle
                    var newService = new FacilityService
                    {
                        Id = Guid.NewGuid(),
                        FacilityId = facility.Id,
                        ServiceName = serviceDto.ServiceName?.Trim() ?? string.Empty,
                        Price = serviceDto.Price,
                        DurationMinutes = 30,
                        BufferMinutes = 0,
                        IsActive = true
                    };
                    context.FacilityServices.Add(newService);
                }
            }
        }

        // ── Personel: tam değiştirme (replace-all) ──
        if (request.Staff != null)
        {
            if (!IsAdmin())
                return StatusCode(403, new { Message = "Personel atamasını yalnızca admin değiştirebilir." });

            // Eski personelleri sil
            context.FacilityStaffs.RemoveRange(facility.StaffMembers);

            // Yeni personelleri ekle
            foreach (var staffName in request.Staff.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                context.FacilityStaffs.Add(new FacilityStaff
                {
                    Id = Guid.NewGuid(),
                    FacilityId = facility.Id,
                    Name = staffName.Trim(),
                    Role = OrduCep.Domain.Enums.FacilityRole.Staff
                });
            }
        }

        await context.SaveChangesAsync(HttpContext.RequestAborted);

        // Yanıt için güncel hizmet ve personel listesini çek
        var updatedServices = await context.FacilityServices
            .Where(s => s.FacilityId == facility.Id && s.IsActive)
            .Select(s => new { id = s.Id, serviceName = s.ServiceName, price = s.Price })
            .ToListAsync();

        var updatedStaff = await context.FacilityStaffs
            .Where(s => s.FacilityId == facility.Id)
            .Select(s => s.Name)
            .ToListAsync();

        var closedDaysList = string.IsNullOrEmpty(facility.ClosedDays)
            ? new List<string>()
            : facility.ClosedDays.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        return Ok(new
        {
            id = facility.Id,
            ordueviId = facility.OrdueviId,
            name = facility.Name,
            category = facility.Category.ToString(),
            appointmentMode = facility.AppointmentMode.ToString(),
            maxConcurrency = facility.MaxConcurrency,
            bufferMinutes = facility.BufferMinutes,
            defaultSlotDurationMinutes = facility.DefaultSlotDurationMinutes,
            openingTime = facility.OpeningTime.ToString(@"hh\:mm"),
            closingTime = facility.ClosingTime.ToString(@"hh\:mm"),
            description = facility.Description,
            image = facility.Image,
            staff = updatedStaff,
            hours = new { closedDays = closedDaysList },
            services = updatedServices,
            message = "Tesis başarıyla güncellendi."
        });
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
        if (!await CanManageFacilityAsync(facilityId, context))
            return StatusCode(403, new { Message = "Bu tesise kaynak ekleme yetkiniz yok." });

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
        if (!await CanManageFacilityAsync(facilityId, context))
            return StatusCode(403, new { Message = "Bu tesisten kaynak silme yetkiniz yok." });

        var resource = await context.Resources
            .FirstOrDefaultAsync(r => r.Id == resourceId && r.FacilityId == facilityId);

        if (resource == null)
            return NotFound(new { Message = "Kaynak bulunamadı." });

        context.Resources.Remove(resource);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    // ──────────────────────────────────────────────
    //  TESİS SORUMLU PERSONEL YÖNETİMİ
    // ──────────────────────────────────────────────

    /// <summary>
    /// Bir tesisin TC ile bağlanmış sorumlu personellerini getirir.
    /// </summary>
    [HttpGet("facilities/{facilityId:guid}/staff")]
    public async Task<IActionResult> GetFacilityStaff(Guid facilityId, [FromServices] IApplicationDbContext context)
    {
        var facilityExists = await context.Facilities.AnyAsync(f => f.Id == facilityId);
        if (!facilityExists)
            return NotFound(new { Message = "Tesis bulunamadı." });

        if (!await CanManageFacilityAsync(facilityId, context))
            return StatusCode(403, new { Message = "Bu tesisin personel listesini görme yetkiniz yok." });

        var staff = await context.FacilityStaffs
            .Where(s => s.FacilityId == facilityId)
            .OrderBy(s => s.Name)
            .ToListAsync();

        var userIds = staff
            .Select(s => s.UserId)
            .Where(id => Guid.TryParse(id, out _))
            .Select(id => Guid.Parse(id!))
            .Distinct()
            .ToList();

        var users = await context.MilitaryIdentityUsers
            .Where(u => userIds.Contains(u.Id))
            .ToListAsync();

        var userMap = users.ToDictionary(u => u.Id.ToString(), StringComparer.OrdinalIgnoreCase);

        return Ok(staff.Select(s =>
        {
            userMap.TryGetValue(s.UserId ?? string.Empty, out var user);

            return new
            {
                s.Id,
                s.FacilityId,
                s.UserId,
                s.Name,
                Role = PersonnelAccessRules.DisplayStaffRole(user?.OwnerRank, s.Role),
                IdentityNumber = MaskIdentity(user?.IdentityNumber),
                PhoneNumber = MaskPhone(user?.PhoneNumber),
                Rank = user?.OwnerRank ?? string.Empty
            };
        }));
    }

    /// <summary>
    /// T.C. kimlik numarası ile kayıtlı kullanıcıyı tesis sorumlusu yapar.
    /// </summary>
    [HttpPost("facilities/{facilityId:guid}/staff")]
    public async Task<IActionResult> AddFacilityStaff(Guid facilityId, [FromBody] AddFacilityStaffRequest request, [FromServices] IApplicationDbContext context)
    {
        request ??= new AddFacilityStaffRequest();
        if (!IsAdmin())
            return StatusCode(403, new { Message = "Sorumlu personel atamasını yalnızca admin yapabilir." });

        var facilityExists = await context.Facilities.AnyAsync(f => f.Id == facilityId);
        if (!facilityExists)
            return NotFound(new { Message = "Tesis bulunamadı." });

        var identityNumber = request.IdentityNumber?.Trim() ?? string.Empty;
        if (identityNumber.Length != 11)
            return BadRequest(new { Message = "Personel T.C. kimlik numarası 11 haneli olmalıdır." });

        var firstName = request.FirstName?.Trim() ?? string.Empty;
        var lastName = request.LastName?.Trim() ?? string.Empty;
        var phoneNumber = request.PhoneNumber?.Trim() ?? string.Empty;
        var personnelStatus = PersonnelAccessRules.NormalizeStatusLabel(request.Role);

        if (!PersonnelAccessRules.IsKnownStatus(personnelStatus))
            return BadRequest(new { Message = "Lütfen personel statüsünü seçiniz." });

        var user = await context.MilitaryIdentityUsers
            .FirstOrDefaultAsync(u => u.IdentityNumber == identityNumber);

        if (user == null)
        {
            if (PersonnelAccessRules.RequiresExistingAccountForAssignment(personnelStatus))
                return BadRequest(new { Message = $"{personnelStatus} statüsündeki personel önce normal kullanıcı hesabı açmalıdır." });

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                return BadRequest(new { Message = "Yeni personel için ad ve soyad zorunludur." });

            user = new MilitaryIdentityUser
            {
                Id = Guid.NewGuid(),
                IdentityNumber = identityNumber,
                PasswordHash = PasswordHashing.Hash(identityNumber),
                FirstName = firstName,
                LastName = lastName,
                PhoneNumber = phoneNumber,
                Relation = "Kendisi",
                OwnerRank = personnelStatus,
                CreatedAtUtc = DateTime.UtcNow
            };

            context.MilitaryIdentityUsers.Add(user);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(firstName))
                user.FirstName = firstName;
            if (!string.IsNullOrWhiteSpace(lastName))
                user.LastName = lastName;
            if (!string.IsNullOrWhiteSpace(phoneNumber))
                user.PhoneNumber = phoneNumber;

            user.OwnerRank = personnelStatus;
        }

        var userId = user.Id.ToString();
        var existing = await context.FacilityStaffs
            .FirstOrDefaultAsync(s => s.FacilityId == facilityId && s.UserId == userId);

        var role = Enum.TryParse<FacilityRole>(request.Role, true, out var parsedRole)
            ? parsedRole
            : FacilityRole.Manager;

        var displayName = BuildFullName(user.FirstName, user.LastName);

        if (existing != null)
        {
            existing.Name = displayName;
            existing.Role = role;
        }
        else
        {
            existing = new FacilityStaff
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                UserId = userId,
                Name = displayName,
                Role = role
            };
            context.FacilityStaffs.Add(existing);
        }

        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            existing.Id,
            existing.FacilityId,
            existing.UserId,
            existing.Name,
            Role = PersonnelAccessRules.DisplayStaffRole(user.OwnerRank, existing.Role),
            IdentityNumber = MaskIdentity(user.IdentityNumber),
            PhoneNumber = MaskPhone(user.PhoneNumber),
            Rank = user.OwnerRank,
            Message = "Sorumlu personel eklendi."
        });
    }

    /// <summary>
    /// Tesis sorumlu personel bağlantısını kaldırır.
    /// </summary>
    [HttpDelete("facilities/{facilityId:guid}/staff/{staffId:guid}")]
    public async Task<IActionResult> DeleteFacilityStaff(Guid facilityId, Guid staffId, [FromServices] IApplicationDbContext context)
    {
        if (!IsAdmin())
            return StatusCode(403, new { Message = "Sorumlu personel atamasını yalnızca admin kaldırabilir." });

        var staff = await context.FacilityStaffs
            .FirstOrDefaultAsync(s => s.Id == staffId && s.FacilityId == facilityId);

        if (staff == null)
            return NotFound(new { Message = "Sorumlu personel bulunamadı." });

        context.FacilityStaffs.Remove(staff);
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
        if (!await CanManageFacilityAsync(facilityId, context))
            return StatusCode(403, new { Message = "Bu tesise hizmet ekleme yetkiniz yok." });

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
        if (!await CanManageFacilityAsync(facilityId, context))
            return StatusCode(403, new { Message = "Bu tesisten hizmet silme yetkiniz yok." });

        var service = await context.FacilityServices
            .FirstOrDefaultAsync(s => s.Id == serviceId && s.FacilityId == facilityId);

        if (service == null)
            return NotFound(new { Message = "Hizmet bulunamadı." });

        context.FacilityServices.Remove(service);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return NoContent();
    }

    /// <summary>
    /// Tek bir hizmetin ad, fiyat ve süresini günceller.
    /// </summary>
    [HttpPut("facilities/{facilityId:guid}/services/{serviceId:guid}")]
    public async Task<IActionResult> EditService(
        Guid facilityId,
        Guid serviceId,
        [FromBody] EditServiceRequest request,
        [FromServices] IApplicationDbContext context)
    {
        if (!await CanManageFacilityAsync(facilityId, context))
            return StatusCode(403, new { Message = "Bu tesisin hizmetlerini düzenleme yetkiniz yok." });

        var service = await context.FacilityServices
            .FirstOrDefaultAsync(s => s.Id == serviceId && s.FacilityId == facilityId);

        if (service == null)
            return NotFound(new { Message = "Hizmet bulunamadı." });

        if (!string.IsNullOrWhiteSpace(request.ServiceName))
            service.ServiceName = request.ServiceName.Trim();

        if (request.Price.HasValue)
            service.Price = request.Price.Value;

        if (request.DurationMinutes.HasValue && request.DurationMinutes.Value > 0)
            service.DurationMinutes = request.DurationMinutes.Value;

        if (request.BufferMinutes.HasValue && request.BufferMinutes.Value >= 0)
            service.BufferMinutes = request.BufferMinutes.Value;

        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            id = service.Id,
            serviceName = service.ServiceName,
            price = service.Price,
            durationMinutes = service.DurationMinutes,
            bufferMinutes = service.BufferMinutes,
            message = "Hizmet başarıyla güncellendi."
        });
    }

    /// <summary>
    /// Bir tesise toplu hizmet ekler. Gelen liste mevcut hizmetlere eklenir (UPSERT değil, append).
    /// </summary>
    [HttpPost("facilities/{facilityId:guid}/services/bulk")]
    public async Task<IActionResult> AddServices(
        Guid facilityId,
        [FromBody] AddServicesRequest request,
        [FromServices] IApplicationDbContext context)
    {
        if (!await CanManageFacilityAsync(facilityId, context))
            return StatusCode(403, new { Message = "Bu tesise hizmet ekleme yetkiniz yok." });

        var facilityExists = await context.Facilities.AnyAsync(f => f.Id == facilityId);
        if (!facilityExists)
            return NotFound(new { Message = "Tesis bulunamadı." });

        if (request.Services == null || request.Services.Count == 0)
            return BadRequest(new { Message = "En az bir hizmet gönderilmelidir." });

        var newServices = request.Services
            .Where(s => !string.IsNullOrWhiteSpace(s.ServiceName))
            .Select(s => new FacilityService
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                ServiceName = s.ServiceName!.Trim(),
                Price = s.Price,
                DurationMinutes = s.DurationMinutes > 0 ? s.DurationMinutes : 30,
                BufferMinutes = s.BufferMinutes >= 0 ? s.BufferMinutes : 0,
                IsActive = true
            })
            .ToList();

        context.FacilityServices.AddRange(newServices);
        await context.SaveChangesAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            added = newServices.Count,
            services = newServices.Select(s => new
            {
                id = s.Id,
                serviceName = s.ServiceName,
                price = s.Price,
                durationMinutes = s.DurationMinutes,
                bufferMinutes = s.BufferMinutes
            }),
            message = $"{newServices.Count} hizmet başarıyla eklendi."
        });
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

public class ManagerCancelReservationRequest
{
    public bool IsSuperAdmin { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid? ResourceId { get; set; }
    public string? Reason { get; set; }
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

public class AddFacilityStaffRequest
{
    public string IdentityNumber { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = "Manager";
}

public class CreateServiceRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public int BufferMinutes { get; set; } = 0;
}

// ── Admin Panel Güncelleme DTO'ları ──

public class UpdateFacilityRequestDto
{
    public string? Name { get; set; }
    public string? Category { get; set; }

    /// <summary>'WalkIn' veya 'Appointment' (AppointmentMode enum string'i)</summary>
    public string? AppointmentMode { get; set; }

    public int? MaxConcurrency { get; set; }
    public int? BufferMinutes { get; set; }
    public int? DefaultSlotDurationMinutes { get; set; }

    /// <summary>Örn: "09:00", "18:00" formatlarında</summary>
    public string? OpeningTime { get; set; }
    public string? ClosingTime { get; set; }

    public string? Description { get; set; }

    /// <summary>Fotoğraf base64 string olarak iletiliyor (örn: data:image/png;base64,iVBO...)</summary>
    public string? Image { get; set; }

    /// <summary>Tesisin personelleri — sadece isim listesi</summary>
    public List<string>? Staff { get; set; }

    /// <summary>Çalışma saatleri ve kapalı günler</summary>
    public FacilityHoursDto? Hours { get; set; }

    /// <summary>Tesiste verilen alt hizmetler ve fiyatları</summary>
    public List<FacilityServiceItemDto>? Services { get; set; }
}

public class FacilityHoursDto
{
    /// <summary>Örn: ["Pzt", "Sal", "Cmt", "Paz"]</summary>
    public List<string>? ClosedDays { get; set; }
}

public class FacilityServiceItemDto
{
    /// <summary>Var olan bir servisi güncellerken dolu gelir; yeni eklenen servis için null/boş gelir.</summary>
    public string? Id { get; set; }
    public string? ServiceName { get; set; }
    public decimal Price { get; set; }
}

/// <summary>Tek bir hizmeti kısmen güncellemek için kullanılır. Gönderilmeyen alanlar değişmez.</summary>
public class EditServiceRequest
{
    public string? ServiceName { get; set; }
    public decimal? Price { get; set; }
    public int? DurationMinutes { get; set; }
    public int? BufferMinutes { get; set; }
}

/// <summary>Birden fazla hizmeti tek seferde eklemek için kullanılır.</summary>
public class AddServicesRequest
{
    public List<NewServiceItemDto> Services { get; set; } = new();
}

public class NewServiceItemDto
{
    public string? ServiceName { get; set; }
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public int BufferMinutes { get; set; } = 0;
}
