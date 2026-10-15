namespace SkillSwap.Application.DTOs.Subscriptions;

public sealed class SubscriptionPlanDto
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int MonthlyLearningMinutes { get; init; }
    public decimal? Price { get; init; }
    public bool PriorityMatching { get; init; }
    public bool AdvancedSearch { get; init; }
    public bool IsActive { get; init; }
}
