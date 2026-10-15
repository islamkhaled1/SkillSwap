using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Subscriptions;

public sealed class UserSubscriptionDto
{
    public long Id { get; init; }
    public Guid UserId { get; init; }
    public int PlanId { get; init; }
    public string PlanCode { get; init; } = string.Empty;
    public string PlanName { get; init; } = string.Empty;
    public int MonthlyLearningMinutes { get; init; }
    public decimal? Price { get; init; }
    public DateTime StartsAtUtc { get; init; }
    public DateTime? EndsAtUtc { get; init; }
    public SubscriptionStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
