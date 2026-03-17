namespace OrduCep.Application.DTOs;

public class TimeSlotDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsAvailable { get; set; }
    
    // Eğer kilitli/alınmış ise kime ait (Sadece yetkili personel/yönetici görür)
    public string OccupiedByUserId { get; set; } = string.Empty;
}
