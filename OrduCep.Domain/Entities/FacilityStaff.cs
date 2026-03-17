namespace OrduCep.Domain.Entities;
using OrduCep.Domain.Enums;


public class FacilityStaff
{
    public Guid Id { get; set; }
    
    // Hangi tesiste/birimde çalıştığı
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;

    // Identity üzerinden gelecek Personel User ID (Sisteme login olan kişi)
    public string UserId { get; set; } = string.Empty;

    // Personel Rolü (Salt Görüntüleyici, Randevu Onaylayıcı vb)
    public FacilityRole Role { get; set; } = FacilityRole.Staff;
}
