using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Favorites;

namespace SkillSwap.Application.Abstractions;

public interface IFavoriteService
{
    Task<Result<IReadOnlyList<FavoriteUserDto>>> GetFavoritesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Result<FavoriteUserDto>> AddFavoriteAsync(
        Guid userId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);

    Task<Result> RemoveFavoriteAsync(
        Guid userId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);
}
