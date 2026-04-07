namespace OrduCep.Domain.Entities;

using OrduCep.Domain.Enums;

/// <summary>
/// Bir tesisteki fiziksel kaynak (koltuk, masa, oda vb.).
/// Eşzamanlılık yönetiminde her kaynak ayrı bir birim olarak ele alınır.
/// </summary>
public class Resource
{
    public Guid Id { get; set; }

    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;

    /// <summary>Örn: "1 Nolu Koltuk", "VIP Masa 3", "Sahne Önü Masa 1"</summary>
    public string Name { get; set; } = string.Empty;

    public ResourceType Type { get; set; } = ResourceType.Generic;

    /// <summary>Kapasite bazlı tesislerde bu kaynağın alabileceği kişi sayısı. Örn: 4 kişilik masa.</summary>
    public int Capacity { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    /// <summary>İsteğe bağlı etiketler: "VIP", "Sahne Önü", "Teras" vb.</summary>
    public string Tags { get; set; } = string.Empty;

    // Navigation
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
