namespace OrduCep.Domain.Entities;
using OrduCep.Domain.Enums;


public class FacilityStaff
{
    public Guid Id { get; set; }
    
    // Hangi tesiste/birimde çalıştığı
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;

    /// <summary>Personel görünen adı. Admin panelinden string olarak gelir.</summary>
    public string Name { get; set; } = string.Empty;

    // Identity üzerinden gelecek Personel User ID (Opsiyonel — sisteme login olmayan personeller için null olabilir)
    public string? UserId { get; set; }

    // Personel Rolü (Salt Görüntüleyici, Randevu Onaylayıcı vb)
    public FacilityRole Role { get; set; } = FacilityRole.Staff;
}
