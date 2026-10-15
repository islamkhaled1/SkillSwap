using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Reviews;

namespace SkillSwap.Tests.Unit;

public class ReviewsControllerTests
{
    private class FakeReviewService : IReviewService
    {
        public Func<Guid, CreateReviewRequest, Task<Result<ReviewDto>>>? OnCreateReview { get; set; }
        public Func<Guid, Task<Result<UserReviewsSummaryDto>>>? OnGetUserReviews { get; set; }

        public Task<Result<ReviewDto>> CreateReviewAsync(Guid reviewerId, CreateReviewRequest request, CancellationToken cancellationToken = default)
        {
            if (OnCreateReview != null) return OnCreateReview(reviewerId, request);
            return Task.FromResult(Result.Success(new ReviewDto { Id = 1, SessionId = request.SessionId, Rating = request.Rating }));
        }

        public Task<Result<UserReviewsSummaryDto>> GetUserReviewsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (OnGetUserReviews != null) return OnGetUserReviews(userId);
            return Task.FromResult(Result.Success(new UserReviewsSummaryDto { UserId = userId, TotalReviews = 0, AverageRating = 0 }));
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
    public async Task CreateReview_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var service = new FakeReviewService();
        var currentUserService = new FakeCurrentUserService { UserId = null };
        var controller = new ReviewsController(service, currentUserService);

        var result = await controller.CreateReview(new CreateReviewRequest { SessionId = 1, Rating = 5 }, default);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task CreateReview_WhenSessionNotFound_ReturnsNotFound()
    {
        var service = new FakeReviewService
        {
            OnCreateReview = (_, _) => Task.FromResult(Result.Failure<ReviewDto>("Session with ID 1 was not found."))
        };
        var currentUserService = new FakeCurrentUserService();
        var controller = new ReviewsController(service, currentUserService);

        var result = await controller.CreateReview(new CreateReviewRequest { SessionId = 1, Rating = 5 }, default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("not found", notFound.Value?.ToString());
    }

    [Fact]
    public async Task CreateReview_WhenNotAuthorized_ReturnsForbid()
    {
        var service = new FakeReviewService
        {
            OnCreateReview = (_, _) => Task.FromResult(Result.Failure<ReviewDto>("You are not authorized to review a session you did not participate in."))
        };
        var currentUserService = new FakeCurrentUserService();
        var controller = new ReviewsController(service, currentUserService);

        var result = await controller.CreateReview(new CreateReviewRequest { SessionId = 1, Rating = 5 }, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CreateReview_WhenSuccessful_ReturnsCreated()
    {
        var service = new FakeReviewService();
        var currentUserService = new FakeCurrentUserService();
        var controller = new ReviewsController(service, currentUserService);

        var result = await controller.CreateReview(new CreateReviewRequest { SessionId = 1, Rating = 5, Comment = "Awesome" }, default);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, created.StatusCode);
    }

    [Fact]
    public async Task GetUserReviews_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var service = new FakeReviewService
        {
            OnGetUserReviews = id => Task.FromResult(Result.Success(new UserReviewsSummaryDto { UserId = id, TotalReviews = 2, AverageRating = 4.5 }))
        };
        var currentUserService = new FakeCurrentUserService();
        var controller = new ReviewsController(service, currentUserService);

        var result = await controller.GetUserReviews(userId, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var summary = Assert.IsType<UserReviewsSummaryDto>(ok.Value);
        Assert.Equal(4.5, summary.AverageRating);
    }
}
