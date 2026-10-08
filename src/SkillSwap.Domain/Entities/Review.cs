namespace SkillSwap.Domain.Entities;

public class Review
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public Guid ReviewerId { get; set; }
    public Guid RevieweeId { get; set; }
    public byte Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Session Session { get; set; } = null!;
}
