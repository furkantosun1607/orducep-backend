namespace OrduCep.Domain.Entities;

public class Facility
{
    public Guid Id { get; set; }
    
    // Hangi orduevine ait olduğu
    public Guid OrdueviId { get; set; }
    public Orduevi Orduevi { get; set; } = null!;

    // Hangi hizmet şablonundan üretildiği
    public Guid FacilityTemplateId { get; set; }
    public FacilityTemplate FacilityTemplate { get; set; } = null!;

    public string Name { get; set; } = string.Empty; // Müdür özelleştirebilir: "1 Nolu Berber" veya "Erbaş Yemekhanesi"
    
    // Randevu ile mi çalışıyor? Eğer false ise, ekranda sadece hizmet listesi gözükecek.
    public bool IsAppointmentBased { get; set; } = true;

    // Çalışma Saatleri ve Randevu
    public TimeSpan OpeningTime { get; set; } = new TimeSpan(8, 0, 0);
    public TimeSpan ClosingTime { get; set; } = new TimeSpan(17, 0, 0);
    
    // 15 dk, 30 dk gibi esnek çalışma saatleri
    public int SlotDurationInMinutes { get; set; } = 30;

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public ICollection<FacilityStaff> StaffMembers { get; set; } = new List<FacilityStaff>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<FacilityService> Services { get; set; } = new List<FacilityService>();
}
