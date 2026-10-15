using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Matching;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Abstractions;

public interface IMatchingService
{
    Task<Result<IReadOnlyList<MatchRecommendationDto>>> GetMatchesAsync(
        Guid currentUserId,
        int? skillId = null,
        UserSkillLevel? minLevel = null,
        CancellationToken cancellationToken = default);
}
