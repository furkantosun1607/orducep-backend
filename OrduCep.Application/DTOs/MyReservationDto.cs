namespace OrduCep.Application.DTOs;

public class MyReservationDto
{
    public Guid Id { get; set; }
    
    public Guid FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public string FacilityIcon { get; set; } = string.Empty;
    
    public Guid? ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    
    public Guid? ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    
    public string Status { get; set; } = string.Empty;
    
    public int GuestCount { get; set; }
}
