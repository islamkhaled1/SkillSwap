using SkillSwap.Domain.Enums;

namespace SkillSwap.Domain.Entities;

public class Notification
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public NotificationKind Type { get; set; }
    public string Payload { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
