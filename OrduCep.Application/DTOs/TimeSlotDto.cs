namespace OrduCep.Application.DTOs;

public class TimeSlotDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsAvailable { get; set; }

    /// <summary>Bu slotta müsait kaynak sayısı</summary>
    public int AvailableCapacity { get; set; }

    /// <summary>Bu slottaki toplam kapasite</summary>
    public int TotalCapacity { get; set; }

    /// <summary>Eğer kilitli/alınmış ise kime ait (Sadece yetkili personel/yönetici görür)</summary>
    public string OccupiedByUserId { get; set; } = string.Empty;
}
