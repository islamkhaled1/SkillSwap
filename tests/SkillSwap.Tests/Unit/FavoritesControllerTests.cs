using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Favorites;

namespace SkillSwap.Tests.Unit;

public class FavoritesControllerTests
{
    private class FakeFavoriteService : IFavoriteService
    {
        public Func<Guid, Task<Result<IReadOnlyList<FavoriteUserDto>>>>? OnGetFavorites { get; set; }
        public Func<Guid, Guid, Task<Result<FavoriteUserDto>>>? OnAddFavorite { get; set; }
        public Func<Guid, Guid, Task<Result>>? OnRemoveFavorite { get; set; }

        public Task<Result<IReadOnlyList<FavoriteUserDto>>> GetFavoritesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (OnGetFavorites != null) return OnGetFavorites(userId);
            return Task.FromResult(Result.Success<IReadOnlyList<FavoriteUserDto>>([]));
        }

        public Task<Result<FavoriteUserDto>> AddFavoriteAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken = default)
        {
            if (OnAddFavorite != null) return OnAddFavorite(userId, targetUserId);
            return Task.FromResult(Result.Success(new FavoriteUserDto { UserId = targetUserId, DisplayName = "Target" }));
        }

        public Task<Result> RemoveFavoriteAsync(Guid userId, Guid targetUserId, CancellationToken cancellationToken = default)
        {
            if (OnRemoveFavorite != null) return OnRemoveFavorite(userId, targetUserId);
            return Task.FromResult(Result.Success());
        }
    }

    private class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public string? Email { get; set; } = "test@example.com";
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }

    [Fact]
    public async Task GetFavorites_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var service = new FakeFavoriteService();
        var currentUserService = new FakeCurrentUserService { UserId = null };
        var controller = new FavoritesController(service, currentUserService);

        var result = await controller.GetFavorites(default);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task AddFavorite_WhenAlreadyExists_ReturnsConflict()
    {
        var service = new FakeFavoriteService
        {
            OnAddFavorite = (_, _) => Task.FromResult(Result.Failure<FavoriteUserDto>("User is already in your favorites list."))
        };
        var currentUserService = new FakeCurrentUserService();
        var controller = new FavoritesController(service, currentUserService);

        var result = await controller.AddFavorite(Guid.NewGuid(), default);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("already", conflict.Value?.ToString());
    }

    [Fact]
    public async Task AddFavorite_WhenSuccessful_ReturnsCreated()
    {
        var targetId = Guid.NewGuid();
        var service = new FakeFavoriteService();
        var currentUserService = new FakeCurrentUserService();
        var controller = new FavoritesController(service, currentUserService);

        var result = await controller.AddFavorite(targetId, default);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
    }

    [Fact]
    public async Task RemoveFavorite_WhenNotFound_ReturnsNotFound()
    {
        var service = new FakeFavoriteService
        {
            OnRemoveFavorite = (_, _) => Task.FromResult(Result.Failure("Favorite record was not found."))
        };
        var currentUserService = new FakeCurrentUserService();
        var controller = new FavoritesController(service, currentUserService);

        var result = await controller.RemoveFavorite(Guid.NewGuid(), default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("not found", notFound.Value?.ToString());
    }

    [Fact]
    public async Task RemoveFavorite_WhenSuccessful_ReturnsNoContent()
    {
        var service = new FakeFavoriteService();
        var currentUserService = new FakeCurrentUserService();
        var controller = new FavoritesController(service, currentUserService);

        var result = await controller.RemoveFavorite(Guid.NewGuid(), default);

        Assert.IsType<NoContentResult>(result);
    }
}
