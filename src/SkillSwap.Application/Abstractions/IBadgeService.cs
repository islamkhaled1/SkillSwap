using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Badges;

namespace SkillSwap.Application.Abstractions;

public interface IBadgeService
{
    Task<Result<IReadOnlyList<BadgeDto>>> GetBadgesAsync(
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<UserBadgeDto>>> GetUserBadgesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<UserBadgeDto>> AwardBadgeAsync(
        Guid userId,
        string badgeCode,
        CancellationToken cancellationToken = default);
}
