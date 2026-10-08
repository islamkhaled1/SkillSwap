namespace SkillSwap.Application.Abstractions;

/// <summary>
/// Provides access to the currently authenticated user''s identity.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}
