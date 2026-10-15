using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Matching;

public sealed class MatchRecommendationDto
{
    public Guid TeacherId { get; init; }
    public string TeacherDisplayName { get; init; } = string.Empty;
    public string? TeacherBio { get; init; }
    public string? TeacherPhotoUrl { get; init; }
    public int SkillId { get; init; }
    public string SkillName { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public UserSkillLevel TeacherSkillLevel { get; init; }
    public bool IsMutualMatch { get; init; }
    public IReadOnlyList<string> MutualSkills { get; init; } = [];
    public int MatchScore { get; init; }
    public IReadOnlyList<AvailabilityOverlapDto> OverlappingAvailability { get; init; } = [];
}
