using System.ComponentModel.DataAnnotations;

namespace SkillSwap.Application.DTOs.Profiles;

/// <summary>
/// Request payload for updating the authenticated user's profile.
/// Server-controlled fields (UserId, IsVerified, CreatedAtUtc) are intentionally excluded.
/// </summary>
public sealed class UpdateProfileRequestDto
{
    [Required(ErrorMessage = "DisplayName is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "DisplayName must be between 1 and 100 characters.")]
    public string DisplayName { get; init; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "Bio cannot exceed 1000 characters.")]
    public string? Bio { get; init; }

    [MaxLength(500, ErrorMessage = "PhotoUrl cannot exceed 500 characters.")]
    public string? PhotoUrl { get; init; }

    [Required(ErrorMessage = "TimeZoneId is required.")]
    [MaxLength(100, ErrorMessage = "TimeZoneId cannot exceed 100 characters.")]
    public string TimeZoneId { get; init; } = "UTC";
}
