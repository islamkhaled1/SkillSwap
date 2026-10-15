using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Profiles;

namespace SkillSwap.Application.Abstractions;

/// <summary>
/// Contract for user profile retrieval and updates.
/// </summary>
public interface IProfileService
{
    Task<Result<UserProfileDto>> GetMyProfileAsync(Guid currentUserId, CancellationToken cancellationToken = default);
    Task<Result<UserProfileDto>> GetProfileByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<UserProfileDto>> UpdateMyProfileAsync(Guid currentUserId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default);
}
