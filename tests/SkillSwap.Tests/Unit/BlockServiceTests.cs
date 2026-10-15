using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class BlockServiceTests
{
    private (BlockService Service, ApplicationDbContext Context, TestDateTimeProvider Dtp) CreateTestHarness()
    {
        var context = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString("N"));
        var dtp = new TestDateTimeProvider { UtcNow = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc) };
        var service = new BlockService(context, dtp);
        return (service, context, dtp);
    }

    [Fact]
    public async Task BlockUser_WhenSelfBlock_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();
        var userId = Guid.NewGuid();

        var result = await service.BlockUserAsync(userId, userId);

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot block yourself", result.Error);
    }

    [Fact]
    public async Task BlockUser_WhenTargetUserNotFound_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var result = await service.BlockUserAsync(userId, targetId);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task BlockUser_WhenAlreadyBlocked_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = userId, DisplayName = "User 1" },
            new UserProfile { UserId = targetId, DisplayName = "Target User" }
        );

        context.UserBlocks.Add(new UserBlock { BlockerId = userId, BlockedId = targetId });
        await context.SaveChangesAsync();

        var result = await service.BlockUserAsync(userId, targetId);

        Assert.False(result.IsSuccess);
        Assert.Contains("already blocked", result.Error);
    }

    [Fact]
    public async Task BlockUser_WhenValid_AddsBlockSuccessfully()
    {
        var (service, context, dtp) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = userId, DisplayName = "User 1" },
            new UserProfile { UserId = targetId, DisplayName = "Target User" }
        );
        await context.SaveChangesAsync();

        var result = await service.BlockUserAsync(userId, targetId);

        Assert.True(result.IsSuccess);
        Assert.Equal(targetId, result.Value!.BlockedId);
        Assert.Equal("Target User", result.Value.DisplayName);
        Assert.Equal(dtp.UtcNow, result.Value.CreatedAtUtc);

        var block = await context.UserBlocks.FirstOrDefaultAsync(b => b.BlockerId == userId && b.BlockedId == targetId);
        Assert.NotNull(block);
    }

    [Fact]
    public async Task UnblockUser_WhenNotFound_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var result = await service.UnblockUserAsync(userId, targetId);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task UnblockUser_WhenValid_RemovesBlockSuccessfully()
    {
        var (service, context, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        context.UserBlocks.Add(new UserBlock { BlockerId = userId, BlockedId = targetId });
        await context.SaveChangesAsync();

        var result = await service.UnblockUserAsync(userId, targetId);

        Assert.True(result.IsSuccess);

        var exists = await context.UserBlocks.AnyAsync(b => b.BlockerId == userId && b.BlockedId == targetId);
        Assert.False(exists);
    }

    [Fact]
    public async Task IsBlocked_WhenBlockedInEitherDirection_ReturnsTrue()
    {
        var (service, context, _) = CreateTestHarness();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var userC = Guid.NewGuid();

        context.UserBlocks.Add(new UserBlock { BlockerId = userA, BlockedId = userB });
        await context.SaveChangesAsync();

        // Forward check: A blocked B
        Assert.True(await service.IsBlockedAsync(userA, userB));
        // Reverse check: two-way block restriction
        Assert.True(await service.IsBlockedAsync(userB, userA));
        // Unrelated user
        Assert.False(await service.IsBlockedAsync(userA, userC));
    }
}
