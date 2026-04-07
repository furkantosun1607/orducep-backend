namespace OrduCep.Domain.Entities;

using OrduCep.Domain.Enums;

/// <summary>
/// Bir orduevine bağlı tesis/birim (berber, pide salonu, meyhane vb.).
/// FacilityTemplate kaldırıldı — kategori ve mod bilgisi artık doğrudan bu entity üzerinde.
/// </summary>
public class Facility
{
    public Guid Id { get; set; }

    // Hangi orduevine ait olduğu
    public Guid OrdueviId { get; set; }
    public Orduevi Orduevi { get; set; } = null!;

    /// <summary>Tesis adı. Örn: "Yıldız Berber Salonu", "Pide Fırını"</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>İşletme kategorisi: Zaman Odaklı / Kapasite Odaklı / Mekan Odaklı</summary>
    public FacilityCategory Category { get; set; } = FacilityCategory.TimeBased;

    /// <summary>Randevu modu: Sadece Randevulu / Karma / Randevusuz (Walk-in)</summary>
    public AppointmentMode AppointmentMode { get; set; } = AppointmentMode.AppointmentOnly;

    /// <summary>
    /// Aynı anda kaç hizmete izin verilir.
    /// Berber: koltuk sayısı, Restoran: masa sayısı veya toplam kişi kapasitesi.
    /// Resource tablosu ile de yönetilebilir (Resource sayısı MaxConcurrency'i belirler).
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>Hizmetler arasındaki varsayılan tampon süresi (dakika). Örn: Temizlik, hazırlık.</summary>
    public int BufferMinutes { get; set; } = 0;

    /// <summary>
    /// Hizmet tanımlanmamışsa kullanılacak varsayılan slot süresi (dakika).
    /// Hizmet tanımlıysa, hizmetin DurationMinutes değeri baskındır.
    /// </summary>
    public int DefaultSlotDurationMinutes { get; set; } = 30;

    // Çalışma Saatleri
    public TimeSpan OpeningTime { get; set; } = new TimeSpan(8, 0, 0);
    public TimeSpan ClosingTime { get; set; } = new TimeSpan(17, 0, 0);

    public bool IsActive { get; set; } = true;

    /// <summary>Tesis açıklaması</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>İkon adı (frontend için)</summary>
    public string Icon { get; set; } = "default-icon";

    // Navigation Properties
    public ICollection<Resource> Resources { get; set; } = new List<Resource>();
    public ICollection<FacilityStaff> StaffMembers { get; set; } = new List<FacilityStaff>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<FacilityService> Services { get; set; } = new List<FacilityService>();
}
