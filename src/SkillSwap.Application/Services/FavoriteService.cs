using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Favorites;
using SkillSwap.Domain.Entities;

namespace SkillSwap.Application.Services;

public sealed class FavoriteService : IFavoriteService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public FavoriteService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<IReadOnlyList<FavoriteUserDto>>> GetFavoritesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var favorites = await _context.Favorites
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var targetUserIds = favorites.Select(f => f.FavoriteUserId).ToList();

        var profiles = await _context.UserProfiles
            .AsNoTracking()
            .Where(p => targetUserIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        var result = favorites.Select(f =>
        {
            profiles.TryGetValue(f.FavoriteUserId, out var profile);
            return new FavoriteUserDto
            {
                UserId = f.FavoriteUserId,
                DisplayName = profile?.DisplayName ?? $"User {f.FavoriteUserId.ToString()[..8]}",
                PhotoUrl = profile?.PhotoUrl,
                Bio = profile?.Bio,
                AddedAtUtc = f.CreatedAtUtc
            };
        }).ToList();

        return Result.Success<IReadOnlyList<FavoriteUserDto>>(result);
    }

    public async Task<Result<FavoriteUserDto>> AddFavoriteAsync(
        Guid userId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (userId == targetUserId)
        {
            return Result.Failure<FavoriteUserDto>("You cannot add yourself to favorites.");
        }

        var targetProfile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == targetUserId, cancellationToken);

        if (targetProfile == null)
        {
            return Result.Failure<FavoriteUserDto>($"User with ID {targetUserId} was not found.");
        }

        var existing = await _context.Favorites
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserId == userId && f.FavoriteUserId == targetUserId, cancellationToken);

        if (existing != null)
        {
            return Result.Failure<FavoriteUserDto>("User is already in your favorites list.");
        }

        var favorite = new Favorite
        {
            UserId = userId,
            FavoriteUserId = targetUserId,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        };

        _context.Favorites.Add(favorite);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new FavoriteUserDto
        {
            UserId = targetUserId,
            DisplayName = targetProfile.DisplayName,
            PhotoUrl = targetProfile.PhotoUrl,
            Bio = targetProfile.Bio,
            AddedAtUtc = favorite.CreatedAtUtc
        });
    }

    public async Task<Result> RemoveFavoriteAsync(
        Guid userId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.FavoriteUserId == targetUserId, cancellationToken);

        if (favorite == null)
        {
            return Result.Failure("Favorite record was not found.");
        }

        _context.Favorites.Remove(favorite);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
