namespace OrduCep.Domain.Entities;

using OrduCep.Domain.Enums;

/// <summary>
/// Bir tesise yapılmış randevu/rezervasyon kaydı.
/// </summary>
public class Reservation
{
    public Guid Id { get; set; }

    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;

    /// <summary>Randevunun atandığı kaynak (koltuk, masa vb.). Null olabilir.</summary>
    public Guid? ResourceId { get; set; }
    public Resource? Resource { get; set; }

    /// <summary>Hangi hizmet için alındı. Null olabilir (genel rezervasyon).</summary>
    public Guid? ServiceId { get; set; }
    public FacilityService? Service { get; set; }

    /// <summary>Randevuyu alan kullanıcı</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Kapasite bazlı tesislerde kaç kişilik (Örn: 4 kişilik masa)</summary>
    public int GuestCount { get; set; } = 1;

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    /// <summary>Randevu durumu — artık type-safe enum</summary>
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    /// <summary>
    /// Aynı anda iki kişinin almasını engellemek için geçici kilitleme mekanizması.
    /// Biri slota tıkladığında 5 dakika boyunca kilitlenir.
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>İsteğe bağlı müşteri notları</summary>
    public string Note { get; set; } = string.Empty;
}
