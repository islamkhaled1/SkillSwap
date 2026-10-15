using System.ComponentModel.DataAnnotations;

namespace SkillSwap.Application.DTOs.Auth;

/// <summary>
/// Request payload for user registration.
/// </summary>
public sealed class RegisterRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [MaxLength(256, ErrorMessage = "Email must not exceed 256 characters.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "DisplayName is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "DisplayName must be between 1 and 100 characters.")]
    public string DisplayName { get; init; } = string.Empty;
}
