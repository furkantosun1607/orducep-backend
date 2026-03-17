namespace OrduCep.Domain.Entities;

public class Orduevi
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    
    // Yöneticisi olan kullanıcının ID'si
    public string AdminUserId { get; set; } = string.Empty;

    // Navigation Properties
    public ICollection<Facility> Facilities { get; set; } = new List<Facility>();
}
