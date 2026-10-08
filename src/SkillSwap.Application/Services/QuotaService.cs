using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Wallets;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Services;

public class QuotaService : IQuotaService
{
    private readonly IApplicationDbContext _context;

    public const int FreeMonthlyMinutes = 180;
    public const int PremiumMonthlyMinutes = 720;

    public QuotaService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetMonthlyLimitMinutesAsync(Guid userId, DateTime targetUtc, CancellationToken cancellationToken = default)
    {
        var subscription = await _context.UserSubscriptions
            .Include(s => s.Plan)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active, cancellationToken);

        if (subscription?.Plan != null)
        {
            return subscription.Plan.MonthlyLearningMinutes;
        }

        return FreeMonthlyMinutes;
    }

    public async Task<int> GetUsedLearningMinutesAsync(Guid userId, DateTime targetUtc, CancellationToken cancellationToken = default)
    {
        var monthStart = new DateTime(targetUtc.Year, targetUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        // Fetch candidate sessions where the user is the learner within the target calendar month
        var sessions = await _context.Sessions
            .AsNoTracking()
            .Where(s => s.LearnerId == userId && s.StartUtc >= monthStart && s.StartUtc < monthEnd)
            .ToListAsync(cancellationToken);

        // Filter sessions that consume learning quota according to Business-Rules.md:
        // Consumed: Scheduled, PendingConfirmation, Completed, Disputed, and Learner NoShow
        // Released / Not Consumed: Cancelled, Expired, Teacher NoShow, Both NoShow
        var consumedMinutes = sessions
            .Where(s => s.Status switch
            {
                SessionStatus.Scheduled => true,
                SessionStatus.PendingConfirmation => true,
                SessionStatus.Completed => true,
                SessionStatus.Disputed => true,
                SessionStatus.NoShow => s.NoShowUserId == userId, // Only counts if learner was the no-show
                SessionStatus.Cancelled => false,
                SessionStatus.Expired => false,
                _ => false
            })
            .Sum(s => (int)s.DurationMinutes);

        return consumedMinutes;
    }

    public async Task<Result<MonthlyQuotaDto>> GetMonthlyQuotaAsync(Guid userId, DateTime targetUtc, CancellationToken cancellationToken = default)
    {
        var limit = await GetMonthlyLimitMinutesAsync(userId, targetUtc, cancellationToken);
        var used = await GetUsedLearningMinutesAsync(userId, targetUtc, cancellationToken);
        var remaining = Math.Max(0, limit - used);

        var subscription = await _context.UserSubscriptions
            .Include(s => s.Plan)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active, cancellationToken);

        var planName = subscription?.Plan?.Name ?? "Free";

        var dto = new MonthlyQuotaDto(
            UserId: userId,
            Year: targetUtc.Year,
            Month: targetUtc.Month,
            UsedMinutes: used,
            MonthlyLimitMinutes: limit,
            RemainingMinutes: remaining,
            PlanName: planName);

        return Result.Success(dto);
    }
}
