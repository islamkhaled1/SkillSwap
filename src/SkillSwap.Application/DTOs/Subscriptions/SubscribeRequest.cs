namespace SkillSwap.Application.DTOs.Subscriptions;

public sealed class SubscribeRequest
{
    public string? PlanCode { get; init; }
    public int? PlanId { get; init; }
}
