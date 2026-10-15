using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class FavoriteServiceTests
{
    private (FavoriteService Service, ApplicationDbContext Context, TestDateTimeProvider Dtp) CreateTestHarness()
    {
        var context = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString("N"));
        var dtp = new TestDateTimeProvider { UtcNow = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc) };
        var service = new FavoriteService(context, dtp);
        return (service, context, dtp);
    }

    [Fact]
    public async Task AddFavorite_WhenSelfFavorite_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();
        var userId = Guid.NewGuid();

        var result = await service.AddFavoriteAsync(userId, userId);

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot add yourself", result.Error);
    }

    [Fact]
    public async Task AddFavorite_WhenTargetUserNotFound_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var result = await service.AddFavoriteAsync(userId, targetId);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task AddFavorite_WhenAlreadyFavorited_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = userId, DisplayName = "User 1" },
            new UserProfile { UserId = targetId, DisplayName = "Target User" }
        );

        context.Favorites.Add(new Favorite { UserId = userId, FavoriteUserId = targetId });
        await context.SaveChangesAsync();

        var result = await service.AddFavoriteAsync(userId, targetId);

        Assert.False(result.IsSuccess);
        Assert.Contains("already in your favorites", result.Error);
    }

    [Fact]
    public async Task AddFavorite_WhenValid_AddsFavoriteSuccessfully()
    {
        var (service, context, dtp) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = userId, DisplayName = "User 1" },
            new UserProfile { UserId = targetId, DisplayName = "Target User", Bio = "Target Bio" }
        );
        await context.SaveChangesAsync();

        var result = await service.AddFavoriteAsync(userId, targetId);

        Assert.True(result.IsSuccess);
        Assert.Equal(targetId, result.Value!.UserId);
        Assert.Equal("Target User", result.Value.DisplayName);
        Assert.Equal("Target Bio", result.Value.Bio);
        Assert.Equal(dtp.UtcNow, result.Value.AddedAtUtc);

        var fav = await context.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.FavoriteUserId == targetId);
        Assert.NotNull(fav);
    }

    [Fact]
    public async Task RemoveFavorite_WhenNotFound_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var result = await service.RemoveFavoriteAsync(userId, targetId);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task RemoveFavorite_WhenValid_RemovesFavoriteSuccessfully()
    {
        var (service, context, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        context.Favorites.Add(new Favorite { UserId = userId, FavoriteUserId = targetId });
        await context.SaveChangesAsync();

        var result = await service.RemoveFavoriteAsync(userId, targetId);

        Assert.True(result.IsSuccess);

        var exists = await context.Favorites.AnyAsync(f => f.UserId == userId && f.FavoriteUserId == targetId);
        Assert.False(exists);
    }

    [Fact]
    public async Task GetFavorites_ReturnsFavoritesWithProfileData()
    {
        var (service, context, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var target1 = Guid.NewGuid();
        var target2 = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = target1, DisplayName = "Favorite 1" },
            new UserProfile { UserId = target2, DisplayName = "Favorite 2" }
        );

        context.Favorites.AddRange(
            new Favorite { UserId = userId, FavoriteUserId = target1, CreatedAtUtc = DateTime.UtcNow.AddHours(-1) },
            new Favorite { UserId = userId, FavoriteUserId = target2, CreatedAtUtc = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var result = await service.GetFavoritesAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal(target2, result.Value[0].UserId);
    }
}
