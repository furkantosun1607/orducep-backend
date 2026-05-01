namespace OrduCep.Domain.Entities;

public class Orduevi
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;

    public int? ScrapedSourceId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string FeaturedImageUrl { get; set; } = string.Empty;
    public string FeaturedImageLocalPath { get; set; } = string.Empty;
    public string Amenities { get; set; } = string.Empty;
    public string ScrapedMetadataJson { get; set; } = string.Empty;
    
    // Yöneticisi olan kullanıcının ID'si
    public string AdminUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation Properties
    public ICollection<Facility> Facilities { get; set; } = new List<Facility>();
}
