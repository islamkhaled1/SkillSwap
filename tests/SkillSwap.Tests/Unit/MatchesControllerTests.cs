using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Matching;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Tests.Unit;

public class MatchesControllerTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public string? Email { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }

    private class FakeMatchingService : IMatchingService
    {
        public Func<Guid, int?, UserSkillLevel?, Task<Result<IReadOnlyList<MatchRecommendationDto>>>>? OnGetMatches { get; set; }

        public Task<Result<IReadOnlyList<MatchRecommendationDto>>> GetMatchesAsync(
            Guid currentUserId,
            int? skillId = null,
            UserSkillLevel? minLevel = null,
            CancellationToken cancellationToken = default)
        {
            if (OnGetMatches != null)
                return OnGetMatches(currentUserId, skillId, minLevel);

            return Task.FromResult(Result.Success<IReadOnlyList<MatchRecommendationDto>>([
                new MatchRecommendationDto
                {
                    TeacherId = Guid.NewGuid(),
                    TeacherDisplayName = "Top Teacher",
                    SkillId = 1,
                    SkillName = "C#",
                    MatchScore = 150
                }
            ]));
        }
    }

    [Fact]
    public async Task GetMatches_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = null };
        var controller = new MatchesController(new FakeMatchingService(), fakeCurrentUserService);

        var response = await controller.GetMatches(null, null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
    }

    [Fact]
    public async Task GetMatches_WhenAuthenticated_ReturnsOkWithRecommendations()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var controller = new MatchesController(new FakeMatchingService(), fakeCurrentUserService);

        var response = await controller.GetMatches(1, UserSkillLevel.Intermediate, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        var matches = Assert.IsAssignableFrom<IReadOnlyList<MatchRecommendationDto>>(okResult.Value);
        Assert.Single(matches);
        Assert.Equal("Top Teacher", matches[0].TeacherDisplayName);
        Assert.Equal(150, matches[0].MatchScore);
    }

    [Fact]
    public async Task GetMatches_WhenSkillNotFound_ReturnsNotFound()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var fakeService = new FakeMatchingService
        {
            OnGetMatches = (_, _, _) => Task.FromResult(Result.Failure<IReadOnlyList<MatchRecommendationDto>>("Active skill with ID 999 was not found."))
        };
        var controller = new MatchesController(fakeService, fakeCurrentUserService);

        var response = await controller.GetMatches(999, null, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(response);
        Assert.Equal("Active skill with ID 999 was not found.", notFound.Value);
    }
}
