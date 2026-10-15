using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Subscriptions;

namespace SkillSwap.Application.Abstractions;

public interface ISubscriptionService
{
    Task<Result<IReadOnlyList<SubscriptionPlanDto>>> GetActivePlansAsync(
        CancellationToken cancellationToken = default);

    Task<Result<UserSubscriptionDto>> GetCurrentSubscriptionAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<UserSubscriptionDto>> SubscribeAsync(
        Guid userId,
        SubscribeRequest request,
        CancellationToken cancellationToken = default);
}
