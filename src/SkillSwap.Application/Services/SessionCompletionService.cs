using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.Common.Options;
using SkillSwap.Application.DTOs.Sessions;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Domain.Exceptions;

namespace SkillSwap.Application.Services;

public class SessionCompletionService : ISessionCompletionService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IWalletLockService _walletLockService;
    private readonly SessionPolicyOptions _options;
    private readonly ILogger<SessionCompletionService> _logger;

    public SessionCompletionService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider,
        IWalletLockService walletLockService,
        IOptions<SessionPolicyOptions> options,
        ILogger<SessionCompletionService> logger)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _walletLockService = walletLockService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<SessionDto>> CompleteSessionAsync(long sessionId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var session = await _context.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session == null)
            throw new NotFoundException(nameof(Session), sessionId);

        if (session.TeacherId != requestingUserId && session.LearnerId != requestingUserId)
            throw new UnauthorizedSessionAccessException(sessionId, requestingUserId);

        return await ExecuteCompletionCoreAsync(session, cancellationToken);
    }

    public async Task<Result<SessionDto>> AutoCompleteSessionAsync(long sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session == null)
            throw new NotFoundException(nameof(Session), sessionId);

        return await ExecuteCompletionCoreAsync(session, cancellationToken);
    }

    private async Task<Result<SessionDto>> ExecuteCompletionCoreAsync(Session session, CancellationToken cancellationToken)
    {
        var sessionId = session.Id;

        if (session.Status == SessionStatus.Completed)
            return Result.Success(MapToDto(session));

        if (session.Status != SessionStatus.Scheduled)
            throw new InvalidSessionStateException(sessionId, session.Status, "Complete");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var affected = await _context.Sessions
            .Where(s => s.Id == sessionId && s.Status == SessionStatus.Scheduled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, SessionStatus.Completed)
                .SetProperty(x => x.UpdatedAtUtc, _dateTimeProvider.UtcNow), cancellationToken);

        if (affected == 0)
        {
            var refreshed = await _context.Sessions.AsNoTracking().FirstAsync(s => s.Id == sessionId, cancellationToken);
            if (refreshed.Status == SessionStatus.Completed)
            {
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(MapToDto(refreshed));
            }

            throw new InvalidSessionStateException(sessionId, refreshed.Status, "Complete");
        }

        var (learnerWallet, teacherWallet) = await _walletLockService.AcquireWalletLocksAsync(session.LearnerId, session.TeacherId, cancellationToken);

        var captureExists = await _context.CreditTransactions
            .AnyAsync(ct => ct.SessionId == sessionId && ct.WalletId == learnerWallet.UserId && ct.Type == CreditTransactionType.Capture, cancellationToken);

        if (!captureExists)
        {
            learnerWallet.HeldMinutes -= session.DurationMinutes;
            learnerWallet.UpdatedAtUtc = _dateTimeProvider.UtcNow;

            _context.CreditTransactions.Add(new CreditTransaction
            {
                WalletId = learnerWallet.UserId,
                SessionId = sessionId,
                Type = CreditTransactionType.Capture,
                AvailableDelta = 0,
                HeldDelta = -session.DurationMinutes,
                CreatedAtUtc = _dateTimeProvider.UtcNow,
                Description = $"Capture for completed session #{sessionId}"
            });
        }

        var earnExists = await _context.CreditTransactions
            .AnyAsync(ct => ct.SessionId == sessionId && ct.WalletId == teacherWallet.UserId && ct.Type == CreditTransactionType.Earn, cancellationToken);

        if (!earnExists)
        {
            teacherWallet.AvailableMinutes += session.DurationMinutes;
            teacherWallet.UpdatedAtUtc = _dateTimeProvider.UtcNow;

            _context.CreditTransactions.Add(new CreditTransaction
            {
                WalletId = teacherWallet.UserId,
                SessionId = sessionId,
                Type = CreditTransactionType.Earn,
                AvailableDelta = session.DurationMinutes,
                HeldDelta = 0,
                CreatedAtUtc = _dateTimeProvider.UtcNow,
                Description = $"Earned from completed session #{sessionId}"
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var updated = await _context.Sessions.AsNoTracking().FirstAsync(s => s.Id == sessionId, cancellationToken);
        return Result.Success(MapToDto(updated));
    }

    public async Task<int> AutoCompleteEligibleSessionsAsync(CancellationToken cancellationToken = default)
    {
        var graceThreshold = _dateTimeProvider.UtcNow.AddMinutes(-_options.AutoCompletionGracePeriodMinutes);

        var eligibleSessionIds = await _context.Sessions
            .AsNoTracking()
            .Where(s => s.Status == SessionStatus.Scheduled && s.EndUtc <= graceThreshold)
            .Select(s => s.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        var completedCount = 0;
        foreach (var id in eligibleSessionIds)
        {
            try
            {
                var result = await AutoCompleteSessionAsync(id, cancellationToken);
                if (result.IsSuccess)
                {
                    completedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while auto-completing session {SessionId}", id);
            }
        }

        return completedCount;
    }

    public async Task<int> ExpirePendingSessionsAsync(CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.UtcNow;
        var creationThreshold = now.AddHours(-_options.PendingConfirmationHours);

        // Expire at the earlier applicable point: 24h passed OR scheduled StartUtc has arrived
        var pendingSessions = await _context.Sessions
            .AsNoTracking()
            .Where(s => s.Status == SessionStatus.PendingConfirmation && (s.CreatedAtUtc <= creationThreshold || s.StartUtc <= now))
            .Select(s => new { s.Id, s.LearnerId, s.DurationMinutes })
            .Take(50)
            .ToListAsync(cancellationToken);

        var expiredCount = 0;
        foreach (var item in pendingSessions)
        {
            try
            {
                var result = await ExpirePendingSessionAsync(item.Id, cancellationToken);
                if (result.IsSuccess)
                {
                    expiredCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while expiring pending confirmation session {SessionId}", item.Id);
            }
        }

        return expiredCount;
    }

    public async Task<Result<SessionDto>> ExpirePendingSessionAsync(long sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session == null)
            throw new NotFoundException(nameof(Session), sessionId);

        if (session.Status == SessionStatus.Expired)
            return Result.Success(MapToDto(session));

        if (session.Status != SessionStatus.PendingConfirmation)
            throw new InvalidSessionStateException(sessionId, session.Status, "ExpirePendingSession");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var affected = await _context.Sessions
            .Where(s => s.Id == sessionId && s.Status == SessionStatus.PendingConfirmation)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, SessionStatus.Expired)
                .SetProperty(x => x.UpdatedAtUtc, _dateTimeProvider.UtcNow), cancellationToken);

        if (affected > 0)
        {
            var learnerWallet = await _walletLockService.AcquireWalletLockAsync(session.LearnerId, cancellationToken);

            var releaseExists = await _context.CreditTransactions
                .AnyAsync(ct => ct.SessionId == sessionId && ct.WalletId == learnerWallet.UserId && ct.Type == CreditTransactionType.Release, cancellationToken);

            if (!releaseExists)
            {
                learnerWallet.HeldMinutes -= session.DurationMinutes;
                learnerWallet.AvailableMinutes += session.DurationMinutes;
                learnerWallet.UpdatedAtUtc = _dateTimeProvider.UtcNow;

                _context.CreditTransactions.Add(new CreditTransaction
                {
                    WalletId = learnerWallet.UserId,
                    SessionId = sessionId,
                    Type = CreditTransactionType.Release,
                    AvailableDelta = session.DurationMinutes,
                    HeldDelta = -session.DurationMinutes,
                    CreatedAtUtc = _dateTimeProvider.UtcNow,
                    Description = $"Release for expired pending confirmation session #{sessionId}"
                });

                await _context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        var updated = await _context.Sessions.AsNoTracking().FirstAsync(s => s.Id == sessionId, cancellationToken);
        return Result.Success(MapToDto(updated));
    }

    private static SessionDto MapToDto(Session s) => new(
        s.Id,
        s.SwapRequestId,
        s.TeacherId,
        s.LearnerId,
        s.SkillId,
        s.StartUtc,
        s.EndUtc,
        s.DurationMinutes,
        s.Mode,
        s.Status,
        s.MeetingUrl,
        s.CancelledById,
        s.CancelledAtUtc,
        s.NoShowUserId,
        s.LearnerJoinedAtUtc,
        s.TeacherJoinedAtUtc,
        s.ReportedById,
        s.ResolvedAtUtc,
        s.ResolutionNote,
        s.CreatedAtUtc,
        s.UpdatedAtUtc);
}
