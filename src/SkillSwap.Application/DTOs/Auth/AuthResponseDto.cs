namespace SkillSwap.Application.DTOs.Auth;

/// <summary>
/// Returned after successful authentication.
/// </summary>
public sealed class AuthResponseDto
{
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public DateTime ExpiresAtUtc { get; init; }
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
}
