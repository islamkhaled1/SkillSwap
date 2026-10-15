using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Blocks;
using SkillSwap.Domain.Entities;

namespace SkillSwap.Application.Services;

public sealed class BlockService : IBlockService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BlockService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<BlockedUserDto>> BlockUserAsync(
        Guid blockerId,
        Guid blockedId,
        CancellationToken cancellationToken = default)
    {
        if (blockerId == blockedId)
        {
            return Result.Failure<BlockedUserDto>("You cannot block yourself.");
        }

        var blockedExists = await _context.UserProfiles
            .AsNoTracking()
            .AnyAsync(p => p.UserId == blockedId, cancellationToken);

        if (!blockedExists)
        {
            return Result.Failure<BlockedUserDto>($"User with ID {blockedId} was not found.");
        }

        var alreadyBlocked = await _context.UserBlocks
            .AsNoTracking()
            .AnyAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId, cancellationToken);

        if (alreadyBlocked)
        {
            return Result.Failure<BlockedUserDto>("User is already blocked.");
        }

        var block = new UserBlock
        {
            BlockerId = blockerId,
            BlockedId = blockedId,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        };

        _context.UserBlocks.Add(block);
        await _context.SaveChangesAsync(cancellationToken);

        var profile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == blockedId, cancellationToken);

        return Result.Success(new BlockedUserDto
        {
            BlockedId = blockedId,
            DisplayName = profile?.DisplayName ?? $"User {blockedId.ToString()[..8]}",
            PhotoUrl = profile?.PhotoUrl,
            CreatedAtUtc = block.CreatedAtUtc
        });
    }

    public async Task<Result> UnblockUserAsync(
        Guid blockerId,
        Guid blockedId,
        CancellationToken cancellationToken = default)
    {
        var block = await _context.UserBlocks
            .FirstOrDefaultAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId, cancellationToken);

        if (block == null)
        {
            return Result.Failure("Block record was not found.");
        }

        _context.UserBlocks.Remove(block);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<BlockedUserDto>>> GetBlockedUsersAsync(
        Guid blockerId,
        CancellationToken cancellationToken = default)
    {
        var blocks = await _context.UserBlocks
            .AsNoTracking()
            .Where(b => b.BlockerId == blockerId)
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var blockedIds = blocks.Select(b => b.BlockedId).ToList();

        var profiles = await _context.UserProfiles
            .AsNoTracking()
            .Where(p => blockedIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        var result = blocks.Select(b =>
        {
            profiles.TryGetValue(b.BlockedId, out var profile);
            return new BlockedUserDto
            {
                BlockedId = b.BlockedId,
                DisplayName = profile?.DisplayName ?? $"User {b.BlockedId.ToString()[..8]}",
                PhotoUrl = profile?.PhotoUrl,
                CreatedAtUtc = b.CreatedAtUtc
            };
        }).ToList();

        return Result.Success<IReadOnlyList<BlockedUserDto>>(result);
    }

    public async Task<bool> IsBlockedAsync(
        Guid userA,
        Guid userB,
        CancellationToken cancellationToken = default)
    {
        return await _context.UserBlocks
            .AsNoTracking()
            .AnyAsync(b => (b.BlockerId == userA && b.BlockedId == userB) ||
                           (b.BlockerId == userB && b.BlockedId == userA),
                      cancellationToken);
    }
}
