using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Reviews;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Services;

public sealed class ReviewService : IReviewService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ReviewService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<ReviewDto>> CreateReviewAsync(
        Guid reviewerId,
        CreateReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return Result.Failure<ReviewDto>("Request cannot be null.");
        }

        if (request.Rating < 1 || request.Rating > 5)
        {
            return Result.Failure<ReviewDto>("Rating must be an integer between 1 and 5.");
        }

        if (request.Comment != null && request.Comment.Length > 1000)
        {
            return Result.Failure<ReviewDto>("Comment must not exceed 1000 characters.");
        }

        var session = await _context.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result.Failure<ReviewDto>($"Session with ID {request.SessionId} was not found.");
        }

        if (session.Status != SessionStatus.Completed)
        {
            return Result.Failure<ReviewDto>(
                $"Reviews are only allowed for completed sessions. Current session status is '{session.Status}'.");
        }

        if (session.LearnerId != reviewerId && session.TeacherId != reviewerId)
        {
            return Result.Failure<ReviewDto>("You are not authorized to review a session you did not participate in.");
        }

        var revieweeId = reviewerId == session.LearnerId ? session.TeacherId : session.LearnerId;

        if (reviewerId == revieweeId)
        {
            return Result.Failure<ReviewDto>("You cannot review yourself.");
        }

        var alreadyReviewed = await _context.Reviews
            .AnyAsync(r => r.SessionId == request.SessionId && r.ReviewerId == reviewerId, cancellationToken);

        if (alreadyReviewed)
        {
            return Result.Failure<ReviewDto>("You have already reviewed this session.");
        }

        var now = _dateTimeProvider.UtcNow;
        var review = new Review
        {
            SessionId = request.SessionId,
            ReviewerId = reviewerId,
            RevieweeId = revieweeId,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            CreatedAtUtc = now
        };

        _context.Reviews.Add(review);

        var notificationPayload = JsonSerializer.Serialize(new
        {
            SessionId = session.Id,
            ReviewerId = reviewerId,
            Rating = request.Rating,
            Message = $"You received a {request.Rating}-star review for session {session.Id}."
        });

        _context.Notifications.Add(new Notification
        {
            UserId = revieweeId,
            Type = NotificationKind.ReviewReceived,
            Payload = notificationPayload,
            IsRead = false,
            CreatedAtUtc = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        var reviewerProfile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == reviewerId, cancellationToken);

        var revieweeProfile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == revieweeId, cancellationToken);

        return Result.Success(new ReviewDto
        {
            Id = review.Id,
            SessionId = review.SessionId,
            ReviewerId = reviewerId,
            ReviewerDisplayName = reviewerProfile?.DisplayName ?? $"User {reviewerId.ToString()[..8]}",
            ReviewerPhotoUrl = reviewerProfile?.PhotoUrl,
            RevieweeId = revieweeId,
            RevieweeDisplayName = revieweeProfile?.DisplayName ?? $"User {revieweeId.ToString()[..8]}",
            RevieweePhotoUrl = revieweeProfile?.PhotoUrl,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAtUtc = review.CreatedAtUtc
        });
    }

    public async Task<Result<UserReviewsSummaryDto>> GetUserReviewsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var reviews = await _context.Reviews
            .AsNoTracking()
            .Where(r => r.RevieweeId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var reviewerIds = reviews.Select(r => r.ReviewerId).Distinct().ToList();

        var profiles = await _context.UserProfiles
            .AsNoTracking()
            .Where(p => reviewerIds.Contains(p.UserId) || p.UserId == userId)
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        profiles.TryGetValue(userId, out var revieweeProfile);

        var dtos = reviews.Select(r =>
        {
            profiles.TryGetValue(r.ReviewerId, out var reviewerProfile);
            return new ReviewDto
            {
                Id = r.Id,
                SessionId = r.SessionId,
                ReviewerId = r.ReviewerId,
                ReviewerDisplayName = reviewerProfile?.DisplayName ?? $"User {r.ReviewerId.ToString()[..8]}",
                ReviewerPhotoUrl = reviewerProfile?.PhotoUrl,
                RevieweeId = r.RevieweeId,
                RevieweeDisplayName = revieweeProfile?.DisplayName ?? $"User {r.RevieweeId.ToString()[..8]}",
                RevieweePhotoUrl = revieweeProfile?.PhotoUrl,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAtUtc = r.CreatedAtUtc
            };
        }).ToList();

        var avgRating = dtos.Count > 0 ? Math.Round(dtos.Average(r => (double)r.Rating), 1) : 0.0;

        return Result.Success(new UserReviewsSummaryDto
        {
            UserId = userId,
            AverageRating = avgRating,
            TotalReviews = dtos.Count,
            Reviews = dtos
        });
    }
}
