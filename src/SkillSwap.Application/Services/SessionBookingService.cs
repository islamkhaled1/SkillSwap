using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Sessions;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Domain.Exceptions;

namespace SkillSwap.Application.Services;

public class SessionBookingService : ISessionBookingService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IWalletLockService _walletLockService;
    private readonly IQuotaService _quotaService;

    public SessionBookingService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider,
        IWalletLockService walletLockService,
        IQuotaService quotaService)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _walletLockService = walletLockService;
        _quotaService = quotaService;
    }

    public async Task<Result<SessionDto>> BookSessionAsync(
        Guid learnerId,
        BookSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.DurationMinutes is not (30 or 60 or 90))
            return Result.Failure<SessionDto>("Invalid session duration. Only 30, 60, and 90 minutes are allowed.");

        if (request.StartUtc <= _dateTimeProvider.UtcNow)
            return Result.Failure<SessionDto>("Session StartUtc must be in the future.");

        var endUtc = request.StartUtc.AddMinutes(request.DurationMinutes);

        var swapRequest = await _context.SwapRequests
            .Include(sr => sr.Skill)
            .AsNoTracking()
            .FirstOrDefaultAsync(sr => sr.Id == request.SwapRequestId, cancellationToken);

        if (swapRequest == null)
            throw new NotFoundException(nameof(SwapRequest), request.SwapRequestId);

        if (swapRequest.Status != SwapRequestStatus.Accepted)
            return Result.Failure<SessionDto>($"SwapRequest must be in Accepted status to schedule sessions. Current status: '{swapRequest.Status}'.");

        // Role contract: Requester is strictly the Learner
        if (learnerId != swapRequest.RequesterId)
            throw new UnauthorizedSessionAccessException(0, learnerId);

        // Receiver is strictly the Teacher
        var teacherId = swapRequest.ReceiverId;
        if (teacherId == learnerId)
            return Result.Failure<SessionDto>("Teacher and Learner cannot be the same user.");

        // Validate SkillId if explicitly supplied in booking request
        if (request.SkillId.HasValue && request.SkillId.Value != swapRequest.SkillId)
            return Result.Failure<SessionDto>($"Session SkillId {request.SkillId.Value} does not match SwapRequest SkillId {swapRequest.SkillId}.");

        // Verify receiver teaches the requested skill
        var teacherOffersSkill = await _context.UserSkills
            .AsNoTracking()
            .AnyAsync(us => us.UserId == teacherId
                         && us.SkillId == swapRequest.SkillId
                         && (us.Type == UserSkillType.Teach || us.Type == UserSkillType.Teaching),
                      cancellationToken);

        if (!teacherOffersSkill)
            return Result.Failure<SessionDto>($"Teacher {teacherId} does not teach skill {swapRequest.SkillId}.");

        // Transaction begins and locks wallets in deterministic order BEFORE quota and balance checks
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var (learnerWallet, teacherWallet) = await _walletLockService.AcquireWalletLocksAsync(learnerId, teacherId, cancellationToken);

        // Recalculate/check monthly quota AFTER acquiring learner wallet lock to serialize concurrent bookings
        var usedQuota = await _quotaService.GetUsedLearningMinutesAsync(learnerId, request.StartUtc, cancellationToken);
        var quotaLimit = await _quotaService.GetMonthlyLimitMinutesAsync(learnerId, request.StartUtc, cancellationToken);
        if (usedQuota + request.DurationMinutes > quotaLimit)
            throw new QuotaExceededException(request.DurationMinutes, usedQuota, quotaLimit);

        if (learnerWallet.AvailableMinutes < request.DurationMinutes)
            throw new InsufficientBalanceException(request.DurationMinutes, learnerWallet.AvailableMinutes);

        var teacherOverlap = await _context.Sessions
            .AsNoTracking()
            .AnyAsync(s => (s.TeacherId == teacherId || s.LearnerId == teacherId)
                        && (s.Status == SessionStatus.Scheduled || s.Status == SessionStatus.PendingConfirmation || s.Status == SessionStatus.Disputed)
                        && s.StartUtc < endUtc
                        && s.EndUtc > request.StartUtc,
                      cancellationToken);

        if (teacherOverlap)
            throw new SessionOverlapException(teacherId, request.StartUtc, endUtc, "Teacher");

        var learnerOverlap = await _context.Sessions
            .AsNoTracking()
            .AnyAsync(s => (s.TeacherId == learnerId || s.LearnerId == learnerId)
                        && (s.Status == SessionStatus.Scheduled || s.Status == SessionStatus.PendingConfirmation || s.Status == SessionStatus.Disputed)
                        && s.StartUtc < endUtc
                        && s.EndUtc > request.StartUtc,
                      cancellationToken);

        if (learnerOverlap)
            throw new SessionOverlapException(learnerId, request.StartUtc, endUtc, "Learner");

        learnerWallet.AvailableMinutes -= request.DurationMinutes;
        learnerWallet.HeldMinutes += request.DurationMinutes;
        learnerWallet.UpdatedAtUtc = _dateTimeProvider.UtcNow;

        var initialStatus = request.RequireTeacherConfirmation
            ? SessionStatus.PendingConfirmation
            : SessionStatus.Scheduled;

        var session = new Session
        {
            SwapRequestId = swapRequest.Id,
            TeacherId = teacherId,
            LearnerId = learnerId,
            SkillId = swapRequest.SkillId,
            StartUtc = request.StartUtc,
            EndUtc = endUtc,
            DurationMinutes = request.DurationMinutes,
            Mode = request.Mode,
            MeetingUrl = request.MeetingUrl,
            Status = initialStatus,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        };

        _context.Sessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        var holdTransaction = new CreditTransaction
        {
            WalletId = learnerWallet.UserId,
            SessionId = session.Id,
            Type = CreditTransactionType.Hold,
            AvailableDelta = -request.DurationMinutes,
            HeldDelta = request.DurationMinutes,
            CreatedAtUtc = _dateTimeProvider.UtcNow,
            Description = $"Escrow hold for {request.DurationMinutes}m session #{session.Id}"
        };

        _context.CreditTransactions.Add(holdTransaction);
        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success(MapToDto(session));
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
