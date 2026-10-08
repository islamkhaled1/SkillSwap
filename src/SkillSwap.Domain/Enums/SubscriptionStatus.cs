namespace SkillSwap.Domain.Enums;

/// <summary>
/// Status of a user subscription.
/// </summary>
public enum SubscriptionStatus : byte
{
    Active = 1,
    Cancelled = 2,
    Expired = 3
}
