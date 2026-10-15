namespace SkillSwap.Application.DTOs.Profiles;

/// <summary>
/// Public user profile data transfer object.
/// Does not expose private account information such as email, wallet balance, or credentials.
/// </summary>
public sealed class UserProfileDto
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Bio { get; init; }
    public string? PhotoUrl { get; init; }
    public string TimeZoneId { get; init; } = "UTC";
    public bool IsVerified { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
