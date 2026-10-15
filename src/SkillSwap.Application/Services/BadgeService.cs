using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Badges;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Services;

public sealed class BadgeService : IBadgeService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BadgeService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<IReadOnlyList<BadgeDto>>> GetBadgesAsync(
        CancellationToken cancellationToken = default)
    {
        var badges = await _context.Badges
            .AsNoTracking()
            .OrderBy(b => b.Id)
            .Select(b => new BadgeDto
            {
                Id = b.Id,
                Code = b.Code,
                Name = b.Name,
                Description = b.Description,
                IconUrl = b.IconUrl
            })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<BadgeDto>>(badges);
    }

    public async Task<Result<IReadOnlyList<UserBadgeDto>>> GetUserBadgesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var userExists = await _context.UserProfiles
            .AsNoTracking()
            .AnyAsync(p => p.UserId == userId, cancellationToken);

        if (!userExists)
        {
            return Result.Failure<IReadOnlyList<UserBadgeDto>>($"User with ID {userId} was not found.");
        }

        var userBadges = await _context.UserBadges
            .AsNoTracking()
            .Include(ub => ub.Badge)
            .Where(ub => ub.UserId == userId)
            .OrderByDescending(ub => ub.AwardedAtUtc)
            .Select(ub => new UserBadgeDto
            {
                BadgeId = ub.BadgeId,
                Code = ub.Badge.Code,
                Name = ub.Badge.Name,
                Description = ub.Badge.Description,
                IconUrl = ub.Badge.IconUrl,
                AwardedAtUtc = ub.AwardedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<UserBadgeDto>>(userBadges);
    }

    public async Task<Result<UserBadgeDto>> AwardBadgeAsync(
        Guid userId,
        string badgeCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(badgeCode))
        {
            return Result.Failure<UserBadgeDto>("Badge code is required.");
        }

        var userExists = await _context.UserProfiles
            .AsNoTracking()
            .AnyAsync(p => p.UserId == userId, cancellationToken);

        if (!userExists)
        {
            return Result.Failure<UserBadgeDto>($"User with ID {userId} was not found.");
        }

        var normalizedCode = badgeCode.Trim().ToUpperInvariant();
        var badge = await _context.Badges
            .FirstOrDefaultAsync(b => b.Code.ToUpper() == normalizedCode, cancellationToken);

        if (badge == null)
        {
            return Result.Failure<UserBadgeDto>($"Badge with code '{badgeCode}' was not found.");
        }

        var alreadyAwarded = await _context.UserBadges
            .AnyAsync(ub => ub.UserId == userId && ub.BadgeId == badge.Id, cancellationToken);

        if (alreadyAwarded)
        {
            return Result.Failure<UserBadgeDto>("User has already been awarded this badge.");
        }

        var now = _dateTimeProvider.UtcNow;
        var userBadge = new UserBadge
        {
            UserId = userId,
            BadgeId = badge.Id,
            AwardedAtUtc = now
        };

        _context.UserBadges.Add(userBadge);

        var payload = JsonSerializer.Serialize(new
        {
            BadgeId = badge.Id,
            BadgeCode = badge.Code,
            BadgeName = badge.Name,
            Message = $"Congratulations! You earned the '{badge.Name}' badge."
        });

        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = NotificationKind.BadgeEarned,
            Payload = payload,
            IsRead = false,
            CreatedAtUtc = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new UserBadgeDto
        {
            BadgeId = badge.Id,
            Code = badge.Code,
            Name = badge.Name,
            Description = badge.Description,
            IconUrl = badge.IconUrl,
            AwardedAtUtc = userBadge.AwardedAtUtc
        });
    }
}
