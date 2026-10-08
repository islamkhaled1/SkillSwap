namespace SkillSwap.Application.Abstractions;

/// <summary>
/// Contract for generating JWT tokens. Implemented in Infrastructure.
/// </summary>
public interface IJwtTokenService
{
    Task<string> GenerateTokenAsync(Guid userId, string email, IEnumerable<string> roles, CancellationToken cancellationToken = default);
}
