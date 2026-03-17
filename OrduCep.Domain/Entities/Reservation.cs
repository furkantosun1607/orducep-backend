namespace OrduCep.Domain.Entities;

public class Reservation
{
    public Guid Id { get; set; }
    
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;

    public string UserId { get; set; } = string.Empty; // Randevuyu alan kişi
    
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    
    public string Status { get; set; } = "Pending"; // Locked, Pending, Approved, Cancelled, Completed 
    
    // Aynı anda iki kişinin almasını engellemek için geçici kilitleme mekanizması
    // Biri slota tıkladığında 5 dakika (veya daha az) boyunca kilitlenir. 
    public DateTime? LockedUntil { get; set; }
    
    // İsteğe bağlı müşteri notları
    public string Note { get; set; } = string.Empty;
}
