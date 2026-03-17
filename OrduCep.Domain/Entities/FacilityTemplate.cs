namespace OrduCep.Domain.Entities;

public class FacilityTemplate
{
    public Guid Id { get; set; }
    
    // Örn: "Berber", "Bayan Kuaförü", "Pide Salonu", "Yemek Salonu", "Çamaşır Odası"
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public string Icon { get; set; } = "default-icon";
    
    // Bu şablon tipinin genel yapısında "Randevu" sistemi uygulanabilir mi?
    // Örn: Yemek salonunda genelde randevu olmaz, ama Alakart veya Berber'de olabilir.
    public bool HasBookingSystem { get; set; } = true;
}
