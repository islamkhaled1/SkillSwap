namespace SkillSwap.Domain.Entities;

public class SubscriptionPlan
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int MonthlyLearningMinutes { get; set; }
    public decimal? Price { get; set; }
    public bool PriorityMatching { get; set; }
    public bool AdvancedSearch { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
}
