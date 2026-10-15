using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.DTOs.Reviews;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class ReviewServiceTests
{
    private (ReviewService Service, ApplicationDbContext Context, TestDateTimeProvider Dtp) CreateTestHarness()
    {
        var context = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString("N"));
        var dtp = new TestDateTimeProvider { UtcNow = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc) };
        var service = new ReviewService(context, dtp);
        return (service, context, dtp);
    }

    [Fact]
    public async Task CreateReview_WhenSessionDoesNotExist_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();
        var reviewerId = Guid.NewGuid();

        var result = await service.CreateReviewAsync(reviewerId, new CreateReviewRequest
        {
            SessionId = 999,
            Rating = 5,
            Comment = "Great session!"
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task CreateReview_WhenSessionNotCompleted_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();
        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var session = new Session
        {
            LearnerId = learnerId,
            TeacherId = teacherId,
            SkillId = 1,
            Status = SessionStatus.Scheduled,
            DurationMinutes = 60,
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1)
        };
        context.Sessions.Add(session);
        await context.SaveChangesAsync();

        var result = await service.CreateReviewAsync(learnerId, new CreateReviewRequest
        {
            SessionId = session.Id,
            Rating = 5
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("only allowed for completed sessions", result.Error);
    }

    [Fact]
    public async Task CreateReview_WhenCallerIsNotParticipant_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();
        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();

        var session = new Session
        {
            LearnerId = learnerId,
            TeacherId = teacherId,
            SkillId = 1,
            Status = SessionStatus.Completed,
            DurationMinutes = 60,
            StartUtc = DateTime.UtcNow.AddHours(-2),
            EndUtc = DateTime.UtcNow.AddHours(-1)
        };
        context.Sessions.Add(session);
        await context.SaveChangesAsync();

        var result = await service.CreateReviewAsync(strangerId, new CreateReviewRequest
        {
            SessionId = session.Id,
            Rating = 4
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("not authorized to review", result.Error);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)6)]
    [InlineData((byte)10)]
    public async Task CreateReview_WhenRatingOutOfBounds_ReturnsFailure(byte invalidRating)
    {
        var (service, _, _) = CreateTestHarness();
        var reviewerId = Guid.NewGuid();

        var result = await service.CreateReviewAsync(reviewerId, new CreateReviewRequest
        {
            SessionId = 1,
            Rating = invalidRating
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("between 1 and 5", result.Error);
    }

    [Fact]
    public async Task CreateReview_WhenCommentExceeds1000Chars_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();
        var reviewerId = Guid.NewGuid();

        var result = await service.CreateReviewAsync(reviewerId, new CreateReviewRequest
        {
            SessionId = 1,
            Rating = 5,
            Comment = new string('x', 1001)
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("1000 characters", result.Error);
    }

    [Fact]
    public async Task CreateReview_WhenDuplicateReviewSubmitted_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();
        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var session = new Session
        {
            LearnerId = learnerId,
            TeacherId = teacherId,
            SkillId = 1,
            Status = SessionStatus.Completed,
            DurationMinutes = 60,
            StartUtc = DateTime.UtcNow.AddHours(-2),
            EndUtc = DateTime.UtcNow.AddHours(-1)
        };
        context.Sessions.Add(session);

        context.Reviews.Add(new Review
        {
            SessionId = session.Id,
            ReviewerId = learnerId,
            RevieweeId = teacherId,
            Rating = 5,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var result = await service.CreateReviewAsync(learnerId, new CreateReviewRequest
        {
            SessionId = session.Id,
            Rating = 4
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("already reviewed", result.Error);
    }

    [Fact]
    public async Task CreateReview_WhenValid_CreatesReviewAndDispatchesNotification()
    {
        var (service, context, dtp) = CreateTestHarness();
        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = learnerId, DisplayName = "Alice Learner" },
            new UserProfile { UserId = teacherId, DisplayName = "Bob Teacher" }
        );

        var session = new Session
        {
            LearnerId = learnerId,
            TeacherId = teacherId,
            SkillId = 1,
            Status = SessionStatus.Completed,
            DurationMinutes = 60,
            StartUtc = dtp.UtcNow.AddHours(-2),
            EndUtc = dtp.UtcNow.AddHours(-1)
        };
        context.Sessions.Add(session);
        await context.SaveChangesAsync();

        var result = await service.CreateReviewAsync(learnerId, new CreateReviewRequest
        {
            SessionId = session.Id,
            Rating = 5,
            Comment = "Phenomenal mentor!"
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(5, result.Value.Rating);
        Assert.Equal("Phenomenal mentor!", result.Value.Comment);
        Assert.Equal("Alice Learner", result.Value.ReviewerDisplayName);
        Assert.Equal("Bob Teacher", result.Value.RevieweeDisplayName);

        var review = await context.Reviews.FirstOrDefaultAsync(r => r.SessionId == session.Id);
        Assert.NotNull(review);
        Assert.Equal(learnerId, review.ReviewerId);
        Assert.Equal(teacherId, review.RevieweeId);

        var notification = await context.Notifications.FirstOrDefaultAsync(n => n.UserId == teacherId);
        Assert.NotNull(notification);
        Assert.Equal(NotificationKind.ReviewReceived, notification.Type);
        Assert.Contains("5-star", notification.Payload);
    }

    [Fact]
    public async Task GetUserReviews_CalculatesAverageRatingAndReturnsSummary()
    {
        var (service, context, _) = CreateTestHarness();
        var teacherId = Guid.NewGuid();
        var learner1 = Guid.NewGuid();
        var learner2 = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = teacherId, DisplayName = "Teacher Terry" },
            new UserProfile { UserId = learner1, DisplayName = "Learner One" },
            new UserProfile { UserId = learner2, DisplayName = "Learner Two" }
        );

        context.Reviews.AddRange(
            new Review
            {
                SessionId = 1,
                ReviewerId = learner1,
                RevieweeId = teacherId,
                Rating = 5,
                Comment = "Perfect!",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
            },
            new Review
            {
                SessionId = 2,
                ReviewerId = learner2,
                RevieweeId = teacherId,
                Rating = 4,
                Comment = "Very good!",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
            }
        );
        await context.SaveChangesAsync();

        var result = await service.GetUserReviewsAsync(teacherId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalReviews);
        Assert.Equal(4.5, result.Value.AverageRating);
        Assert.Equal(2, result.Value.Reviews.Count);
    }
}
