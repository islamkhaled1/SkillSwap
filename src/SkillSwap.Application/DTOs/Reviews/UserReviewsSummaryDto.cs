namespace SkillSwap.Application.DTOs.Reviews;

public sealed class UserReviewsSummaryDto
{
    public Guid UserId { get; init; }
    public double AverageRating { get; init; }
    public int TotalReviews { get; init; }
    public IReadOnlyList<ReviewDto> Reviews { get; init; } = [];
}
