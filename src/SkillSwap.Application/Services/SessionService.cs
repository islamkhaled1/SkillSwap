using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Sessions;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Domain.Exceptions;

namespace SkillSwap.Application.Services;

public class SessionService : ISessionService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IWalletLockService _walletLockService;

    public SessionService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider,
        IWalletLockService walletLockService)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _walletLockService = walletLockService;
    }

    public async Task<Result<SessionDto>> GetSessionByIdAsync(long sessionId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var session = await _context.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
            throw new NotFoundException(nameof(Session), sessionId);

        if (session.TeacherId != requestingUserId && session.LearnerId != requestingUserId)
            throw new UnauthorizedSessionAccessException(sessionId, requestingUserId);

        return Result.Success(MapToDto(session));
    }

    public async Task<Result<IReadOnlyList<SessionDto>>> GetUserSessionsAsync(Guid userId, SessionStatus? statusFilter = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Sessions
            .AsNoTracking()
            .Where(s => s.TeacherId == userId || s.LearnerId == userId);

        if (statusFilter.HasValue)
        {
            query = query.Where(s => s.Status == statusFilter.Value);
        }

        var sessions = await query
            .OrderByDescending(s => s.StartUtc)
            .Select(s => MapToDto(s))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SessionDto>>(sessions);
    }

    public async Task<Result<SessionDto>> ConfirmSessionAsync(long sessionId, Guid teacherId, CancellationToken cancellationToken = default)
    {
        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session == null)
            throw new NotFoundException(nameof(Session), sessionId);

        if (session.TeacherId != teacherId)
            throw new UnauthorizedSessionAccessException(sessionId, teacherId);

        if (session.Status == SessionStatus.Scheduled)
            return Result.Success(MapToDto(session));

        if (session.Status != SessionStatus.PendingConfirmation)
            throw new InvalidSessionStateException(sessionId, session.Status, "Confirm");

        var affected = await _context.Sessions
            .Where(s => s.Id == sessionId && s.Status == SessionStatus.PendingConfirmation)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, SessionStatus.Scheduled)
                .SetProperty(x => x.UpdatedAtUtc, _dateTimeProvider.UtcNow), cancellationToken);

        if (affected == 0)
        {
            var refreshed = await _context.Sessions.AsNoTracking().FirstAsync(s => s.Id == sessionId, cancellationToken);
            if (refreshed.Status == SessionStatus.Scheduled)
                return Result.Success(MapToDto(refreshed));

            throw new InvalidSessionStateException(sessionId, refreshed.Status, "Confirm");
        }

        var confirmed = await _context.Sessions.AsNoTracking().FirstAsync(s => s.Id == sessionId, cancellationToken);
        return Result.Success(MapToDto(confirmed));
    }

    public async Task<Result<SessionDto>> CancelSessionAsync(long sessionId, Guid requestingUserId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var session = await _context.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session == null)
            throw new NotFoundException(nameof(Session), sessionId);

        if (session.TeacherId != requestingUserId && session.LearnerId != requestingUserId)
            throw new UnauthorizedSessionAccessException(sessionId, requestingUserId);

        if (session.Status == SessionStatus.Cancelled)
            return Result.Success(MapToDto(session));

        if (session.Status is SessionStatus.Completed or SessionStatus.NoShow or SessionStatus.Expired)
            throw new InvalidSessionStateException(sessionId, session.Status, "Cancel");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var affected = await _context.Sessions
            .Where(s => s.Id == sessionId && (s.Status == SessionStatus.Scheduled || s.Status == SessionStatus.PendingConfirmation))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, SessionStatus.Cancelled)
                .SetProperty(x => x.CancelledById, requestingUserId)
                .SetProperty(x => x.CancelledAtUtc, _dateTimeProvider.UtcNow)
                .SetProperty(x => x.UpdatedAtUtc, _dateTimeProvider.UtcNow), cancellationToken);

        if (affected == 0)
        {
            var refreshed = await _context.Sessions.AsNoTracking().FirstAsync(s => s.Id == sessionId, cancellationToken);
            if (refreshed.Status == SessionStatus.Cancelled)
            {
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(MapToDto(refreshed));
            }
            throw new InvalidSessionStateException(sessionId, refreshed.Status, "Cancel");
        }

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
                Description = $"Hold release for cancelled session #{sessionId}"
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var updated = await _context.Sessions.AsNoTracking().FirstAsync(s => s.Id == sessionId, cancellationToken);
        return Result.Success(MapToDto(updated));
    }

    public async Task<Result<SessionDto>> MarkJoinedAsync(long sessionId, Guid requestingUserId, CancellationToken cancellationToken = default)
    {
        var session = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session == null)
            throw new NotFoundException(nameof(Session), sessionId);

        if (session.TeacherId != requestingUserId && session.LearnerId != requestingUserId)
            throw new UnauthorizedSessionAccessException(sessionId, requestingUserId);

        var now = _dateTimeProvider.UtcNow;
        if (session.LearnerId == requestingUserId)
        {
            session.LearnerJoinedAtUtc = now;
        }
        else
        {
            session.TeacherJoinedAtUtc = now;
        }

        session.UpdatedAtUtc = now;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToDto(session));
    }

    public Task<Result<SessionDto>> MarkNoShowAsync(
        long sessionId,
        Guid requestingUserId,
        NoShowParty noShowParty,
        CancellationToken cancellationToken = default)
    {
        return MarkNoShowAsync(sessionId, requestingUserId, noShowParty, isAdmin: false, cancellationToken);
    }

    public async Task<Result<SessionDto>> MarkNoShowAsync(
        long sessionId,
        Guid requestingUserId,
        NoShowParty noShowParty,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session == null)
            throw new NotFoundException(nameof(Session), sessionId);

        if (!isAdmin)
        {
            if (session.TeacherId != requestingUserId && session.LearnerId != requestingUserId)
                throw new UnauthorizedSessionAccessException(sessionId, requestingUserId);

            // A participant may only report the OTHER party as no-show
            if (requestingUserId == session.LearnerId && noShowParty != NoShowParty.Teacher)
                throw new DomainException("A learner can only report the teacher as a no-show.");

            if (requestingUserId == session.TeacherId && noShowParty != NoShowParty.Learner)
                throw new DomainException("A teacher can only report the learner as a no-show.");

            if (noShowParty == NoShowParty.Both)
                throw new DomainException("An individual participant cannot report 'Both' as no-show.");
        }

        if (session.Status == SessionStatus.NoShow)
            return Result.Success(MapToDto(session));

        if (session.Status != SessionStatus.Scheduled)
            throw new InvalidSessionStateException(sessionId, session.Status, "MarkNoShow");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        Guid? noShowUserId = noShowParty switch
        {
            NoShowParty.Learner => session.LearnerId,
            NoShowParty.Teacher => session.TeacherId,
            NoShowParty.Both => null,
            _ => null
        };

        var affected = await _context.Sessions
            .Where(s => s.Id == sessionId && s.Status == SessionStatus.Scheduled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, SessionStatus.NoShow)
                .SetProperty(x => x.NoShowUserId, noShowUserId)
                .SetProperty(x => x.UpdatedAtUtc, _dateTimeProvider.UtcNow), cancellationToken);

        if (affected == 0)
        {
            var refreshed = await _context.Sessions.AsNoTracking().FirstAsync(s => s.Id == sessionId, cancellationToken);
            if (refreshed.Status == SessionStatus.NoShow)
            {
                await transaction.CommitAsync(cancellationToken);
                return Result.Success(MapToDto(refreshed));
            }
            throw new InvalidSessionStateException(sessionId, refreshed.Status, "MarkNoShow");
        }

        if (noShowParty == NoShowParty.Learner)
        {
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
                    Description = $"Capture on learner no-show for session #{sessionId}"
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
                    Description = $"Earned from learner no-show for session #{sessionId}"
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        else
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
                    Description = $"Release on {noShowParty} no-show for session #{sessionId}"
                });

                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        var updated = await _context.Sessions.AsNoTracking().FirstAsync(s => s.Id == sessionId, cancellationToken);
        return Result.Success(MapToDto(updated));
    }

    public async Task<Result<SessionDto>> ReportDisputeAsync(long sessionId, Guid requestingUserId, string reason, CancellationToken cancellationToken = default)
    {
        var session = await _context.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session == null)
            throw new NotFoundException(nameof(Session), sessionId);

        if (session.TeacherId != requestingUserId && session.LearnerId != requestingUserId)
            throw new UnauthorizedSessionAccessException(sessionId, requestingUserId);

        if (session.Status == SessionStatus.Disputed)
            return Result.Success(MapToDto(session));

        if (session.Status is SessionStatus.Cancelled or SessionStatus.Expired)
            throw new InvalidSessionStateException(sessionId, session.Status, "Dispute");

        var affected = await _context.Sessions
            .Where(s => s.Id == sessionId && s.Status == SessionStatus.Scheduled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, SessionStatus.Disputed)
                .SetProperty(x => x.ReportedById, requestingUserId)
                .SetProperty(x => x.ResolutionNote, reason)
                .SetProperty(x => x.UpdatedAtUtc, _dateTimeProvider.UtcNow), cancellationToken);

        if (affected == 0)
        {
            var refreshed = await _context.Sessions.AsNoTracking().FirstAsync(s => s.Id == sessionId, cancellationToken);
            if (refreshed.Status == SessionStatus.Disputed)
                return Result.Success(MapToDto(refreshed));

            throw new InvalidSessionStateException(sessionId, refreshed.Status, "Dispute");
        }

        var updated = await _context.Sessions.AsNoTracking().FirstAsync(s => s.Id == sessionId, cancellationToken);
        return Result.Success(MapToDto(updated));
    }

    public async Task<Result<SessionDto>> ResolveDisputeAsync(long sessionId, Guid adminId, bool awardTeacher, string resolutionNote, CancellationToken cancellationToken = default)
    {
        if (adminId == Guid.Empty)
            throw new UnauthorizedSessionAccessException(sessionId, adminId);

        var session = await _context.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session == null)
            throw new NotFoundException(nameof(Session), sessionId);

        // Participants cannot arbitrate their own dispute as admin
        if (session.TeacherId == adminId || session.LearnerId == adminId)
            throw new UnauthorizedSessionAccessException(sessionId, adminId);

        if (session.Status != SessionStatus.Disputed)
            throw new InvalidSessionStateException(sessionId, session.Status, "ResolveDispute");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        if (awardTeacher)
        {
            var (learnerWallet, teacherWallet) = await _walletLockService.AcquireWalletLocksAsync(session.LearnerId, session.TeacherId, cancellationToken);

            learnerWallet.HeldMinutes -= session.DurationMinutes;
            learnerWallet.UpdatedAtUtc = _dateTimeProvider.UtcNow;

            teacherWallet.AvailableMinutes += session.DurationMinutes;
            teacherWallet.UpdatedAtUtc = _dateTimeProvider.UtcNow;

            _context.CreditTransactions.Add(new CreditTransaction
            {
                WalletId = learnerWallet.UserId,
                SessionId = sessionId,
                Type = CreditTransactionType.Capture,
                AvailableDelta = 0,
                HeldDelta = -session.DurationMinutes,
                CreatedAtUtc = _dateTimeProvider.UtcNow,
                Description = $"Capture on dispute resolution for session #{sessionId}"
            });

            _context.CreditTransactions.Add(new CreditTransaction
            {
                WalletId = teacherWallet.UserId,
                SessionId = sessionId,
                Type = CreditTransactionType.Earn,
                AvailableDelta = session.DurationMinutes,
                HeldDelta = 0,
                CreatedAtUtc = _dateTimeProvider.UtcNow,
                Description = $"Earned on dispute resolution for session #{sessionId}"
            });

            await _context.Sessions
                .Where(s => s.Id == sessionId && s.Status == SessionStatus.Disputed)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, SessionStatus.Completed)
                    .SetProperty(x => x.ResolvedAtUtc, _dateTimeProvider.UtcNow)
                    .SetProperty(x => x.ResolutionNote, resolutionNote)
                    .SetProperty(x => x.UpdatedAtUtc, _dateTimeProvider.UtcNow), cancellationToken);
        }
        else
        {
            var learnerWallet = await _walletLockService.AcquireWalletLockAsync(session.LearnerId, cancellationToken);

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
                Description = $"Release on dispute refund for session #{sessionId}"
            });

            await _context.Sessions
                .Where(s => s.Id == sessionId && s.Status == SessionStatus.Disputed)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, SessionStatus.Cancelled)
                    .SetProperty(x => x.ResolvedAtUtc, _dateTimeProvider.UtcNow)
                    .SetProperty(x => x.ResolutionNote, resolutionNote)
                    .SetProperty(x => x.UpdatedAtUtc, _dateTimeProvider.UtcNow), cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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


