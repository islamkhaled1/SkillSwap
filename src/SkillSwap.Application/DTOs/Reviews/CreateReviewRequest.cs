using System.ComponentModel.DataAnnotations;

namespace SkillSwap.Application.DTOs.Reviews;

public sealed class CreateReviewRequest
{
    [Range(1, long.MaxValue, ErrorMessage = "A valid SessionId is required.")]
    public long SessionId { get; init; }

    [Range(1, 5, ErrorMessage = "Rating must be an integer between 1 and 5.")]
    public byte Rating { get; init; }

    [StringLength(1000, ErrorMessage = "Comment must not exceed 1000 characters.")]
    public string? Comment { get; init; }
}
