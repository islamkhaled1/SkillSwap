using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Badges;

namespace SkillSwap.Tests.Unit;

public class BadgesControllerTests
{
    private class FakeBadgeService : IBadgeService
    {
        public Func<Task<Result<IReadOnlyList<BadgeDto>>>>? OnGetBadges { get; set; }
        public Func<Guid, Task<Result<IReadOnlyList<UserBadgeDto>>>>? OnGetUserBadges { get; set; }

        public Task<Result<IReadOnlyList<BadgeDto>>> GetBadgesAsync(CancellationToken cancellationToken = default)
        {
            if (OnGetBadges != null) return OnGetBadges();
            return Task.FromResult(Result.Success<IReadOnlyList<BadgeDto>>([]));
        }

        public Task<Result<IReadOnlyList<UserBadgeDto>>> GetUserBadgesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (OnGetUserBadges != null) return OnGetUserBadges(userId);
            return Task.FromResult(Result.Success<IReadOnlyList<UserBadgeDto>>([]));
        }

        public Task<Result<UserBadgeDto>> AwardBadgeAsync(Guid userId, string badgeCode, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public async Task GetAllBadges_ReturnsOk()
    {
        var service = new FakeBadgeService
        {
            OnGetBadges = () => Task.FromResult(Result.Success<IReadOnlyList<BadgeDto>>([
                new BadgeDto { Id = 1, Code = "B1", Name = "Badge 1", Description = "Desc 1" }
            ]))
        };
        var controller = new BadgesController(service);

        var result = await controller.GetAllBadges(default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var badges = Assert.IsAssignableFrom<IReadOnlyList<BadgeDto>>(ok.Value);
        Assert.Single(badges);
    }

    [Fact]
    public async Task GetUserBadges_WhenUserNotFound_ReturnsNotFound()
    {
        var service = new FakeBadgeService
        {
            OnGetUserBadges = id => Task.FromResult(Result.Failure<IReadOnlyList<UserBadgeDto>>($"User with ID {id} was not found."))
        };
        var controller = new BadgesController(service);

        var result = await controller.GetUserBadges(Guid.NewGuid(), default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("not found", notFound.Value?.ToString());
    }

    [Fact]
    public async Task GetUserBadges_WhenSuccessful_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var service = new FakeBadgeService
        {
            OnGetUserBadges = id => Task.FromResult(Result.Success<IReadOnlyList<UserBadgeDto>>([
                new UserBadgeDto { BadgeId = 1, Code = "B1", Name = "Badge 1", Description = "Desc", AwardedAtUtc = DateTime.UtcNow }
            ]))
        };
        var controller = new BadgesController(service);

        var result = await controller.GetUserBadges(userId, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var badges = Assert.IsAssignableFrom<IReadOnlyList<UserBadgeDto>>(ok.Value);
        Assert.Single(badges);
    }
}
