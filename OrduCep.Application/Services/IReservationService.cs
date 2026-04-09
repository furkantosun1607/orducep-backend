using OrduCep.Application.DTOs;

namespace OrduCep.Application.Services;

public interface IReservationService
{
    /// <summary>
    /// Belirtilen tesis ve tarih için müsait zaman dilimlerini getirir.
    /// serviceId verilirse, slot süreleri hizmet süresine göre hesaplanır.
    /// </summary>
    Task<List<TimeSlotDto>> GetAvailableTimeSlotsAsync(Guid facilityId, DateTime date, Guid? serviceId = null);

    /// <summary>
    /// Zaman dilimini kullanıcı için 5 dakika kilitler.
    /// Kapasite kontrolü yapılır: n &lt; K ise kilit oluşturulur.
    /// </summary>
    Task<bool> LockTimeSlotAsync(Guid facilityId, DateTime startTime, Guid? serviceId, Guid? resourceId, string userId, int guestCount = 1);

    /// <summary>
    /// Kilitli randevuyu onaylar (Pending durumuna çeker).
    /// </summary>
    Task<bool> ConfirmReservationAsync(Guid facilityId, DateTime startTime, string userId);

    /// <summary>
    /// Kullanıcının geçmiş ve gelecek tüm randevularını getirir.
    /// </summary>
    Task<List<MyReservationDto>> GetUserReservationsAsync(string userId);

    /// <summary>
    /// Belirtilen randevuyu iptal eder ve boşa çıkarır.
    /// </summary>
    Task<bool> CancelReservationAsync(Guid reservationId, string userId);

    /// <summary>
    /// Belirtilen tarih aralığı için gün gün müsaitlik takvimini getirir.
    /// </summary>
    Task<List<CalendarDayDto>> GetFacilityAvailabilityCalendarAsync(Guid facilityId, DateTime startDate, DateTime endDate, Guid? serviceId = null);
}
