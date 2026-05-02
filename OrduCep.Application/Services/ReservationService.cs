using Microsoft.EntityFrameworkCore;
using OrduCep.Application.DTOs;
using OrduCep.Application.Interfaces;
using OrduCep.Domain.Entities;
using OrduCep.Domain.Enums;

namespace OrduCep.Application.Services;

public class ReservationService : IReservationService
{
    private readonly IApplicationDbContext _context;

    public ReservationService(IApplicationDbContext context)
    {
        _context = context;
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

    /// <inheritdoc />
    public async Task<List<TimeSlotDto>> GetAvailableTimeSlotsAsync(Guid facilityId, DateTime date, Guid? serviceId = null, Guid? resourceId = null)
    {
        var facility = await _context.Facilities
            .FirstOrDefaultAsync(f => f.Id == facilityId && f.IsActive);

        if (facility == null)
            throw new Exception("Birim bulunamadı veya kapalı.");

        if (facility.AppointmentMode == AppointmentMode.WalkInOnly)
            throw new Exception("Bu tesis sadece walk-in kabul eder, randevu sistemi yoktur.");

        int slotDuration = facility.DefaultSlotDurationMinutes;
        const int bufferMinutes = 0;

        if (slotDuration <= 0)
            slotDuration = 30; // Güvenlik: sıfır ya da negatif süre engelle

        var activeResourceCount = await _context.Resources
            .CountAsync(r => r.FacilityId == facilityId && r.IsActive);

        var selectedResourceExists = !resourceId.HasValue || await _context.Resources
            .AnyAsync(r => r.Id == resourceId.Value && r.FacilityId == facilityId && r.IsActive);

        if (!selectedResourceExists)
            return new List<TimeSlotDto>();

        int totalCapacity = resourceId.HasValue
            ? 1
            : activeResourceCount > 0 ? activeResourceCount : facility.MaxConcurrency;

        // Hedef günün başlangıç ve bitiş zamanı
        var dayStart = date.Date + facility.OpeningTime;
        var dayEnd = date.Date + facility.ClosingTime;

        // O gün için olan bütün aktif randevular
        var currentReservations = await _context.Reservations
            .Where(r => r.FacilityId == facilityId &&
                        r.StartTime >= dayStart &&
                        r.EndTime <= dayEnd &&
                        r.Status != ReservationStatus.Cancelled)
            .ToListAsync();

        var slots = new List<TimeSlotDto>();
        var cursor = dayStart;
        var now = DateTime.Now;

        while (cursor + TimeSpan.FromMinutes(slotDuration) <= dayEnd)
        {
            var slotEnd = cursor.AddMinutes(slotDuration);
            var slotEndWithBuffer = cursor.AddMinutes(slotDuration + bufferMinutes);

            int overlappingCount = currentReservations.Count(r =>
                r.StartTime < slotEndWithBuffer && r.EndTime > cursor &&
                (!resourceId.HasValue || r.ResourceId == resourceId.Value) &&
                (r.Status == ReservationStatus.Approved ||
                 r.Status == ReservationStatus.Pending ||
                 (r.Status == ReservationStatus.Locked && r.LockedUntil > DateTime.UtcNow)));

            int availableCapacity = totalCapacity - overlappingCount;
            bool isFutureSlot = ToBusinessLocalTime(cursor) > now;

            slots.Add(new TimeSlotDto
            {
                StartTime = cursor,
                EndTime = slotEnd,
                IsAvailable = isFutureSlot && availableCapacity > 0,
                AvailableCapacity = isFutureSlot ? Math.Max(0, availableCapacity) : 0,
                TotalCapacity = totalCapacity,
                OccupiedByUserId = string.Empty
            });

            cursor = slotEnd;
        }

        return slots;
    }

    /// <inheritdoc />
    public async Task<bool> LockTimeSlotAsync(
        Guid facilityId,
        DateTime startTime,
        Guid? serviceId,
        Guid? resourceId,
        string userId,
        int guestCount = 1)
    {
        var facility = await _context.Facilities
            .FirstOrDefaultAsync(f => f.Id == facilityId && f.IsActive);

        if (facility == null)
            return false;

        if (facility.AppointmentMode == AppointmentMode.WalkInOnly)
            return false;

        if (IsPastOrCurrentBusinessTime(startTime))
            return false;

        int duration = facility.DefaultSlotDurationMinutes;
        const int buffer = 0;

        if (duration <= 0)
            duration = 30;

        var endTime = startTime.AddMinutes(duration);
        var endTimeWithBuffer = startTime.AddMinutes(duration + buffer);

        // Toplam kapasite
        var activeResourceCount = await _context.Resources
            .CountAsync(r => r.FacilityId == facilityId && r.IsActive);

        int totalCapacity = activeResourceCount > 0 ? activeResourceCount : facility.MaxConcurrency;

        // Eğer belirli bir resource isteniyorsa, o resource müsait mi kontrol et
        if (resourceId.HasValue)
        {
            var resourceExists = await _context.Resources
                .AnyAsync(r => r.Id == resourceId.Value && r.FacilityId == facilityId && r.IsActive);

            if (!resourceExists)
                return false;

            var resourceConflict = await _context.Reservations
                .AnyAsync(r => r.FacilityId == facilityId &&
                               r.ResourceId == resourceId.Value &&
                               r.StartTime < endTimeWithBuffer &&
                               r.EndTime > startTime &&
                               r.Status != ReservationStatus.Cancelled &&
                               (r.Status != ReservationStatus.Locked || r.LockedUntil > DateTime.UtcNow));

            if (resourceConflict)
                return false; // Bu kaynak dolu
        }
        else
        {
            // Berber/personel seçilmediyse tesis genel kapasitesine göre kontrol et.
            int overlappingCount = await _context.Reservations
                .CountAsync(r => r.FacilityId == facilityId &&
                                 r.StartTime < endTimeWithBuffer &&
                                 r.EndTime > startTime &&
                                 r.Status != ReservationStatus.Cancelled &&
                                 (r.Status != ReservationStatus.Locked || r.LockedUntil > DateTime.UtcNow));

            if (overlappingCount >= totalCapacity)
                return false; // Kapasite dolu

            if (activeResourceCount > 0)
            {
                // Resource belirtilmemişse, müsait bir kaynak otomatik ata
                var busyResourceIds = await _context.Reservations
                    .Where(r => r.FacilityId == facilityId &&
                                r.ResourceId != null &&
                                r.StartTime < endTimeWithBuffer &&
                                r.EndTime > startTime &&
                                r.Status != ReservationStatus.Cancelled &&
                                (r.Status != ReservationStatus.Locked || r.LockedUntil > DateTime.UtcNow))
                    .Select(r => r.ResourceId!.Value)
                    .ToListAsync();

                var freeResource = await _context.Resources
                    .FirstOrDefaultAsync(r => r.FacilityId == facilityId &&
                                              r.IsActive &&
                                              !busyResourceIds.Contains(r.Id));

                if (freeResource == null)
                    return false;

                resourceId = freeResource.Id;
            }
        }

        // Kilitleme kaydı oluştur
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            ResourceId = resourceId,
            ServiceId = serviceId,
            UserId = userId,
            GuestCount = guestCount,
            StartTime = startTime,
            EndTime = endTime,
            Status = ReservationStatus.Locked,
            LockedUntil = DateTime.UtcNow.AddMinutes(5)
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync(default);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmReservationAsync(Guid facilityId, DateTime startTime, string userId)
    {
        var lockedReservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.FacilityId == facilityId &&
                                      r.StartTime == startTime &&
                                      r.UserId == userId &&
                                      r.Status == ReservationStatus.Locked);

        if (lockedReservation == null)
            return false;

        if (IsPastOrCurrentBusinessTime(lockedReservation.StartTime))
        {
            lockedReservation.Status = ReservationStatus.Cancelled;
            lockedReservation.LockedUntil = null;
            await _context.SaveChangesAsync(default);
            return false;
        }

        // Süre dolmuşsa iptal et
        if (lockedReservation.LockedUntil < DateTime.UtcNow)
        {
            lockedReservation.Status = ReservationStatus.Cancelled;
            await _context.SaveChangesAsync(default);
            return false;
        }

        // Randevuyu onayla
        lockedReservation.Status = ReservationStatus.Pending;
        lockedReservation.LockedUntil = null;

        await _context.SaveChangesAsync(default);
        return true;
    }

    /// <inheritdoc />
    public async Task<List<MyReservationDto>> GetUserReservationsAsync(string userId)
    {
        var reservations = await _context.Reservations
            .Include(r => r.Facility)
            .Include(r => r.Service)
            .Include(r => r.Resource)
            .Where(r => r.UserId == userId && r.Status != ReservationStatus.Locked)
            .OrderByDescending(r => r.StartTime)
            .Select(r => new MyReservationDto
            {
                Id = r.Id,
                FacilityId = r.FacilityId,
                FacilityName = r.Facility.Name,
                FacilityIcon = r.Facility.Icon,
                ServiceId = r.ServiceId,
                ServiceName = r.Service != null ? r.Service.ServiceName : string.Empty,
                ResourceId = r.ResourceId,
                ResourceName = r.Resource != null ? r.Resource.Name : string.Empty,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                Status = r.Status.ToString(),
                GuestCount = r.GuestCount
            })
            .ToListAsync();

        return reservations;
    }

    /// <inheritdoc />
    public async Task<bool> CancelReservationAsync(Guid reservationId, string userId)
    {
        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.UserId == userId);

        if (reservation == null)
            return false;

        // Geçmiş randevuları iptal etmeyi engelle
        if (IsPastOrCurrentBusinessTime(reservation.StartTime))
            return false;

        if (reservation.Status == ReservationStatus.Cancelled)
            return true; // Zaten iptal edilmiş

        reservation.Status = ReservationStatus.Cancelled;
        await _context.SaveChangesAsync(default);

        return true;
    }

    /// <inheritdoc />
    public async Task<List<CalendarDayDto>> GetFacilityAvailabilityCalendarAsync(Guid facilityId, DateTime startDate, DateTime endDate, Guid? serviceId = null, Guid? resourceId = null)
    {
        var days = new List<CalendarDayDto>();
        var currentDate = startDate.Date;
        var end = endDate.Date;

        while (currentDate <= end)
        {
            var slots = await GetAvailableTimeSlotsAsync(facilityId, currentDate, serviceId, resourceId);
            int availableSlots = slots.Count(s => s.IsAvailable);

            days.Add(new CalendarDayDto
            {
                Date = currentDate,
                IsAvailable = availableSlots > 0,
                AvailableSlotsCount = availableSlots
            });

            currentDate = currentDate.AddDays(1);
        }

        return days;
    }
}
