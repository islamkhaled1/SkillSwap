namespace SkillSwap.Application.DTOs.Auth;

/// <summary>
/// Request payload for user login.
/// </summary>
public sealed class LoginRequestDto
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
