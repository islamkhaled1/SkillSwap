using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Auth;

namespace SkillSwap.Application.Abstractions;

/// <summary>
/// Contract for authentication and user onboarding operations.
/// </summary>
public interface IAuthService
{
    Task<Result<AuthResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
}
