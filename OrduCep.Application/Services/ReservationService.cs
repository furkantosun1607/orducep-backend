using Microsoft.EntityFrameworkCore;
using OrduCep.Application.DTOs;
using OrduCep.Application.Interfaces;
using OrduCep.Domain.Entities;

namespace OrduCep.Application.Services;

public class ReservationService : IReservationService
{
    private readonly IApplicationDbContext _context;

    public ReservationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<TimeSlotDto>> GetAvailableTimeSlotsAsync(Guid facilityId, DateTime date)
    {
        var facility = await _context.Facilities
            .FirstOrDefaultAsync(f => f.Id == facilityId && f.IsActive);

        if (facility == null)
            throw new Exception("Birim bulunamadı veya kapalı.");

        // Hedef günün başlangıç ve bitiş zamanı
        var startDateTime = date.Date + facility.OpeningTime;
        var endDateTime = date.Date + facility.ClosingTime;

        // O gün için olan bütün randevular (İptal edilmemiş olanlar)
        // Eğer Status Locked ise, kilit süresinin dolup dolmadığına da bakacağız
        var currentReservations = await _context.Reservations
            .Where(r => r.FacilityId == facilityId &&
                        r.StartTime >= startDateTime && 
                        r.EndTime <= endDateTime &&
                        r.Status != "Cancelled")
            .ToListAsync();

        var slots = new List<TimeSlotDto>();

        // Zamanı SlotDuration kadar arttırarak gün içindeki döngüyü oluşturuyoruz
        var currentSlotMark = startDateTime;
        
        while (currentSlotMark < endDateTime)
        {
            var nextSlotMark = currentSlotMark.AddMinutes(facility.SlotDurationInMinutes);
            
            // Bu zaman aralığında aktif bir rezervasyon/kilit var mı?
            var overlappingReservation = currentReservations
                .FirstOrDefault(r => 
                    (r.StartTime < nextSlotMark && r.EndTime > currentSlotMark) && // Zamanlar kesişiyor mu
                    (r.Status == "Approved" || r.Status == "Pending" || 
                    (r.Status == "Locked" && r.LockedUntil > DateTime.UtcNow)) // Eğer Locked ise ve süresi geçmediyse hala dolu
                );

            slots.Add(new TimeSlotDto
            {
                StartTime = currentSlotMark,
                EndTime = nextSlotMark,
                IsAvailable = overlappingReservation == null, // Eğer çakışan kayıt yoksa müsaittir (Açıktır)
                OccupiedByUserId = overlappingReservation != null ? overlappingReservation.UserId : string.Empty
            });

            currentSlotMark = nextSlotMark;
        }

        return slots;
    }

    public async Task<bool> LockTimeSlotAsync(Guid facilityId, DateTime startTime, DateTime endTime, string userId)
    {
        // 1. Double-Booking kontrolü
        // Çakışan bir kayıt var mı? Onaylanmış, Beklemede veya Kilitli(ve süresi bitmemiş) ise KİLİTLEYEMEZSİN.
        var conflict = await _context.Reservations
            .AnyAsync(r => r.FacilityId == facilityId &&
                           r.StartTime < endTime && r.EndTime > startTime &&
                           r.Status != "Cancelled" &&
                           (r.Status != "Locked" || r.LockedUntil > DateTime.UtcNow));

        if (conflict)
        {
            return false; // Başkası almış veya işlem yapıyor, kilitleme başarısız
        }

        // 2. Eğer çakışma yoksa, kendimiz için bir "Locked" kaydı oluşturuyoruz
        // Sistemin yoğunluğuna göre saniyelik çakışmaları DbTransaction ile de güçlendirebiliriz ilerde.
        var newReservation = new Reservation
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            UserId = userId,
            StartTime = startTime,
            EndTime = endTime,
            Status = "Locked",
            LockedUntil = DateTime.UtcNow.AddMinutes(5) // 5 dakika mühlet veriyoruz
        };

        _context.Reservations.Add(newReservation);
        await _context.SaveChangesAsync(default);

        return true;
    }

    public async Task<bool> ConfirmReservationAsync(Guid facilityId, DateTime startTime, string userId)
    {
        // Kullanıcının daha önce kilitlediği geçici kaydı bul
        var lockedReservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.FacilityId == facilityId &&
                                      r.StartTime == startTime &&
                                      r.UserId == userId &&
                                      r.Status == "Locked");

        if (lockedReservation == null)
            return false; // Ya kilit süresi bittiği için silinmiş, ya da böyle bir kilit hiç var olmadı

        // Eğer süre dolmuşsa ama sistem hala silmemişse de kontrol et
        if (lockedReservation.LockedUntil < DateTime.UtcNow)
        {
            // Zaman geçmiş, iptale çek
            lockedReservation.Status = "Cancelled";
            await _context.SaveChangesAsync(default);
            return false;
        }

        // Her şey geçerliyse ve kullanıcı zamanında tıkladıysa Randevuyu onayla!
        lockedReservation.Status = "Pending"; // veya duruma göre sisteminiz otomatik Approved yapabilir
        lockedReservation.LockedUntil = null; // Kilit kalktı, tamamen bu kullanıcının
        
        await _context.SaveChangesAsync(default);
        return true;
    }
}
