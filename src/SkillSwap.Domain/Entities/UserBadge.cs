namespace SkillSwap.Domain.Entities;

public class UserBadge
{
    public Guid UserId { get; set; }
    public int BadgeId { get; set; }
    public DateTime AwardedAtUtc { get; set; } = DateTime.UtcNow;

    public Badge Badge { get; set; } = null!;
}
