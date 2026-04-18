namespace OrduCep.Domain.Entities;

/// <summary>
/// Bir tesise ait hizmet tanımı. Örn: Saç Kesimi (30 dk), Sakal Tıraşı (15 dk), Kıymalı Pide vb.
/// </summary>
public class FacilityService
{
    public Guid Id { get; set; }

    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;

    /// <summary>Hizmet adı. Örn: "Saç Kesimi", "Sakal Tıraşı", "Kıymalı Pide"</summary>
    public string ServiceName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    /// <summary>Hizmetin standart süresi (dakika). Randevu süresi hesaplamasında kullanılır.</summary>
    public int DurationMinutes { get; set; } = 30;

    /// <summary>Bu hizmet sonrası tampon süresi (dakika). Temizlik, hazırlık vb.</summary>
    public int BufferMinutes { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}
