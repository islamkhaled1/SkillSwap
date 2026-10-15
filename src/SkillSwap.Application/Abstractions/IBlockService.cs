using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Blocks;

namespace SkillSwap.Application.Abstractions;

public interface IBlockService
{
    Task<Result<BlockedUserDto>> BlockUserAsync(
        Guid blockerId,
        Guid blockedId,
        CancellationToken cancellationToken = default);

    Task<Result> UnblockUserAsync(
        Guid blockerId,
        Guid blockedId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<BlockedUserDto>>> GetBlockedUsersAsync(
        Guid blockerId,
        CancellationToken cancellationToken = default);

    Task<bool> IsBlockedAsync(
        Guid userA,
        Guid userB,
        CancellationToken cancellationToken = default);
}
