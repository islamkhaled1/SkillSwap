namespace SkillSwap.Application.DTOs.Reviews;

public sealed class ReviewDto
{
    public long Id { get; init; }
    public long SessionId { get; init; }
    public Guid ReviewerId { get; init; }
    public string ReviewerDisplayName { get; init; } = string.Empty;
    public string? ReviewerPhotoUrl { get; init; }
    public Guid RevieweeId { get; init; }
    public string RevieweeDisplayName { get; init; } = string.Empty;
    public string? RevieweePhotoUrl { get; init; }
    public byte Rating { get; init; }
    public string? Comment { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
