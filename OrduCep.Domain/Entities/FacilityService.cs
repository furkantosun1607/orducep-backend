namespace OrduCep.Domain.Entities;

public class FacilityService
{
    public Guid Id { get; set; }
    
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;

    // Örn: Saç Kesimi, Sakal Tıraşı, Kıymalı Pide
    public string ServiceName { get; set; } = string.Empty;
    
    public decimal Price { get; set; }
    
    // Bu hizmet ne kadar sürer? Bazen ServiceDuration ile Facility SlotDuration etkileşmek zorundadır.
    public int EstimatedDurationInMinutes { get; set; } = 30;
    
    public bool IsActive { get; set; } = true;
}
