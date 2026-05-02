namespace OrduCep.Domain.Entities;

public class MilitaryIdentityUser
{
    public Guid Id { get; set; }
    public string IdentityNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// Kendisi (hak sahibi) veya yakınlık derecesi: Eş, Çocuk, Anne, Baba vb.
    /// </summary>
    public string Relation { get; set; } = string.Empty;

    // Asıl hak sahibi bilgileri (yakını ise doldurulur)
    public string OwnerTcNo { get; set; } = string.Empty;
    public string OwnerFirstName { get; set; } = string.Empty;
    public string OwnerLastName { get; set; } = string.Empty;
    public string OwnerRank { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
