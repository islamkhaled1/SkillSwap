namespace SkillSwap.Application.DTOs.Auth;

/// <summary>
/// Request payload for user registration. Validation logic will be added later.
/// </summary>
public sealed class RegisterRequestDto
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
