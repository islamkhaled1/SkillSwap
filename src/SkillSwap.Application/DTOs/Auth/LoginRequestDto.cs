using System.ComponentModel.DataAnnotations;

namespace SkillSwap.Application.DTOs.Auth;

/// <summary>
/// Request payload for user login.
/// </summary>
public sealed class LoginRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; init; } = string.Empty;
}
