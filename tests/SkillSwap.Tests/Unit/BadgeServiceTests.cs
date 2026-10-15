using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class BadgeServiceTests
{
    private (BadgeService Service, ApplicationDbContext Context, TestDateTimeProvider Dtp) CreateTestHarness()
    {
        var context = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString("N"));
        var dtp = new TestDateTimeProvider { UtcNow = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc) };
        var service = new BadgeService(context, dtp);
        return (service, context, dtp);
    }

    [Fact]
    public async Task GetBadges_ReturnsAllBadges()
    {
        var (service, context, _) = CreateTestHarness();

        context.Badges.AddRange(
            new Badge { Id = 1, Code = "FIRST_SESSION", Name = "First Session", Description = "Completed first session" },
            new Badge { Id = 2, Code = "TOP_RATED", Name = "Top Rated", Description = "Maintained 5 star rating" }
        );
        await context.SaveChangesAsync();

        var result = await service.GetBadgesAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("FIRST_SESSION", result.Value[0].Code);
    }

    [Fact]
    public async Task GetUserBadges_WhenUserNotFound_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();

        var result = await service.GetUserBadgesAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task GetUserBadges_ReturnsBadgesAwardedToUser()
    {
        var (service, context, dtp) = CreateTestHarness();
        var userId = Guid.NewGuid();

        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Achiever Alice" });

        var badge = new Badge { Id = 1, Code = "SUPER_TEACHER", Name = "Super Teacher", Description = "Taught 10 sessions" };
        context.Badges.Add(badge);

        context.UserBadges.Add(new UserBadge
        {
            UserId = userId,
            BadgeId = badge.Id,
            AwardedAtUtc = dtp.UtcNow.AddDays(-1),
            Badge = badge
        });
        await context.SaveChangesAsync();

        var result = await service.GetUserBadgesAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("SUPER_TEACHER", result.Value![0].Code);
        Assert.Equal(dtp.UtcNow.AddDays(-1), result.Value[0].AwardedAtUtc);
    }

    [Fact]
    public async Task AwardBadge_WhenAlreadyAwarded_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();
        var userId = Guid.NewGuid();

        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Alice" });

        var badge = new Badge { Id = 1, Code = "EARLY_ADOPTER", Name = "Early Adopter", Description = "Joined early" };
        context.Badges.Add(badge);

        context.UserBadges.Add(new UserBadge { UserId = userId, BadgeId = badge.Id });
        await context.SaveChangesAsync();

        var result = await service.AwardBadgeAsync(userId, "EARLY_ADOPTER");

        Assert.False(result.IsSuccess);
        Assert.Contains("already been awarded", result.Error);
    }

    [Fact]
    public async Task AwardBadge_WhenValid_AwardsBadgeAndDispatchesNotification()
    {
        var (service, context, dtp) = CreateTestHarness();
        var userId = Guid.NewGuid();

        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Alice" });

        var badge = new Badge { Id = 10, Code = "FIRST_SWAP", Name = "First Swap", Description = "Completed first swap" };
        context.Badges.Add(badge);
        await context.SaveChangesAsync();

        var result = await service.AwardBadgeAsync(userId, "FIRST_SWAP");

        Assert.True(result.IsSuccess);
        Assert.Equal("FIRST_SWAP", result.Value!.Code);
        Assert.Equal(dtp.UtcNow, result.Value.AwardedAtUtc);

        var ub = await context.UserBadges.FirstOrDefaultAsync(u => u.UserId == userId && u.BadgeId == badge.Id);
        Assert.NotNull(ub);

        var notification = await context.Notifications.FirstOrDefaultAsync(n => n.UserId == userId);
        Assert.NotNull(notification);
        Assert.Equal(NotificationKind.BadgeEarned, notification.Type);
        Assert.Contains("First Swap", notification.Payload);
    }
}
