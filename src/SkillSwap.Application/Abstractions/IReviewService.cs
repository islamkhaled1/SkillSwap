using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Reviews;

namespace SkillSwap.Application.Abstractions;

public interface IReviewService
{
    Task<Result<ReviewDto>> CreateReviewAsync(
        Guid reviewerId,
        CreateReviewRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<UserReviewsSummaryDto>> GetUserReviewsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
