using SkillSwap.Domain.Enums;

namespace SkillSwap.Domain.Entities;

public class UserSubscription
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public int PlanId { get; set; }
    public DateTime StartsAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndsAtUtc { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public SubscriptionPlan Plan { get; set; } = null!;
}
