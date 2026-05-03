namespace OrduCep.Domain.Entities;

public class VoiceSession
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Channel { get; set; } = "web";
    public Guid? OrdueviId { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime ExpiresAtUtc { get; set; }
    public string Status { get; set; } = "created";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
