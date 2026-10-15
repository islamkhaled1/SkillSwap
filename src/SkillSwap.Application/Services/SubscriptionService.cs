using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Subscriptions;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Services;

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SubscriptionService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<IReadOnlyList<SubscriptionPlanDto>>> GetActivePlansAsync(
        CancellationToken cancellationToken = default)
    {
        var plans = await _context.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Id)
            .Select(p => new SubscriptionPlanDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                MonthlyLearningMinutes = p.MonthlyLearningMinutes,
                Price = p.Price,
                PriorityMatching = p.PriorityMatching,
                AdvancedSearch = p.AdvancedSearch,
                IsActive = p.IsActive
            })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SubscriptionPlanDto>>(plans);
    }

    public async Task<Result<UserSubscriptionDto>> GetCurrentSubscriptionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var userExists = await _context.UserProfiles
            .AsNoTracking()
            .AnyAsync(p => p.UserId == userId, cancellationToken);

        if (!userExists)
        {
            return Result.Failure<UserSubscriptionDto>($"User with ID {userId} was not found.");
        }

        var activeSub = await _context.UserSubscriptions
            .Include(s => s.Plan)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active, cancellationToken);

        if (activeSub != null)
        {
            return Result.Success(MapToDto(activeSub));
        }

        // Fallback: If user somehow lacks an active subscription record, assign default FREE plan
        var freePlan = await _context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Code == "FREE", cancellationToken);

        if (freePlan == null)
        {
            return Result.Failure<UserSubscriptionDto>("Default subscription plan not found.");
        }

        var now = _dateTimeProvider.UtcNow;
        var newSub = new UserSubscription
        {
            UserId = userId,
            PlanId = freePlan.Id,
            StartsAtUtc = now,
            EndsAtUtc = null,
            Status = SubscriptionStatus.Active,
            CreatedAtUtc = now,
            Plan = freePlan
        };

        _context.UserSubscriptions.Add(newSub);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToDto(newSub));
    }

    public async Task<Result<UserSubscriptionDto>> SubscribeAsync(
        Guid userId,
        SubscribeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || (string.IsNullOrWhiteSpace(request.PlanCode) && !request.PlanId.HasValue))
        {
            return Result.Failure<UserSubscriptionDto>("Plan code or plan ID must be specified.");
        }

        var userExists = await _context.UserProfiles
            .AsNoTracking()
            .AnyAsync(p => p.UserId == userId, cancellationToken);

        if (!userExists)
        {
            return Result.Failure<UserSubscriptionDto>($"User with ID {userId} was not found.");
        }

        SubscriptionPlan? targetPlan = null;
        if (!string.IsNullOrWhiteSpace(request.PlanCode))
        {
            var normalized = request.PlanCode.Trim().ToUpperInvariant();
            targetPlan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.Code.ToUpper() == normalized && p.IsActive, cancellationToken);
        }
        else if (request.PlanId.HasValue)
        {
            targetPlan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.Id == request.PlanId.Value && p.IsActive, cancellationToken);
        }

        if (targetPlan == null)
        {
            return Result.Failure<UserSubscriptionDto>("Selected subscription plan was not found or is inactive.");
        }

        var currentActive = await _context.UserSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active, cancellationToken);

        // Idempotent: if already active on the same plan, return it
        if (currentActive != null && currentActive.PlanId == targetPlan.Id)
        {
            return Result.Success(MapToDto(currentActive));
        }

        var now = _dateTimeProvider.UtcNow;
        if (currentActive != null)
        {
            currentActive.Status = SubscriptionStatus.Cancelled;
            currentActive.EndsAtUtc = now;
        }

        var newSub = new UserSubscription
        {
            UserId = userId,
            PlanId = targetPlan.Id,
            StartsAtUtc = now,
            EndsAtUtc = null,
            Status = SubscriptionStatus.Active,
            CreatedAtUtc = now,
            Plan = targetPlan
        };

        _context.UserSubscriptions.Add(newSub);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToDto(newSub));
    }

    private static UserSubscriptionDto MapToDto(UserSubscription sub)
    {
        return new UserSubscriptionDto
        {
            Id = sub.Id,
            UserId = sub.UserId,
            PlanId = sub.PlanId,
            PlanCode = sub.Plan?.Code ?? string.Empty,
            PlanName = sub.Plan?.Name ?? string.Empty,
            MonthlyLearningMinutes = sub.Plan?.MonthlyLearningMinutes ?? 0,
            Price = sub.Plan?.Price,
            StartsAtUtc = sub.StartsAtUtc,
            EndsAtUtc = sub.EndsAtUtc,
            Status = sub.Status,
            CreatedAtUtc = sub.CreatedAtUtc
        };
    }
}
