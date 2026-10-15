namespace SkillSwap.Application.DTOs.Badges;

public sealed class UserBadgeDto
{
    public int BadgeId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? IconUrl { get; init; }
    public DateTime AwardedAtUtc { get; init; }
}
