using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.DTOs.Sessions;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Domain.Exceptions;
using SkillSwap.Infrastructure.Services;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class SessionBookingTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(90)]
    public async Task BookSession_AllowedDurations_SucceedAndHoldCredits(int durationMinutes)
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var request = new BookSessionRequest(
            SwapRequestId: swapReq.Id,
            StartUtc: dtp.UtcNow.AddDays(1),
            DurationMinutes: (short)durationMinutes,
            Mode: SessionMode.Online,
            RequireTeacherConfirmation: false,
            MeetingUrl: "https://meet.skillswap.test/room1"
        );

        var result = await bookingService.BookSessionAsync(learner.Id, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(durationMinutes, result.Value.DurationMinutes);
        Assert.Equal(SessionStatus.Scheduled, result.Value.Status);
        Assert.Equal(request.StartUtc.AddMinutes(durationMinutes), result.Value.EndUtc);

        // Verify learner wallet state
        var learnerWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(180 - durationMinutes, learnerWallet.AvailableMinutes);
        Assert.Equal(durationMinutes, learnerWallet.HeldMinutes);

        // Verify credit ledger
        var ledgerHold = await context.CreditTransactions.FirstOrDefaultAsync(ct => ct.SessionId == result.Value.Id && ct.Type == CreditTransactionType.Hold);
        Assert.NotNull(ledgerHold);
        Assert.Equal(-durationMinutes, ledgerHold.AvailableDelta);
        Assert.Equal(durationMinutes, ledgerHold.HeldDelta);
        Assert.Equal(learner.Id, ledgerHold.WalletId);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(45)]
    [InlineData(120)]
    public async Task BookSession_DisallowedDurations_Rejected(int invalidDuration)
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var request = new BookSessionRequest(
            SwapRequestId: swapReq.Id,
            StartUtc: dtp.UtcNow.AddDays(1),
            DurationMinutes: (short)invalidDuration,
            Mode: SessionMode.Online,
            RequireTeacherConfirmation: false
        );

        var result = await bookingService.BookSessionAsync(learner.Id, request);

        Assert.False(result.IsSuccess);
        Assert.Contains("Only 30, 60, and 90 minutes are allowed", result.Error);
    }

    [Fact]
    public async Task BookSession_PastDate_Rejected()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var request = new BookSessionRequest(
            SwapRequestId: swapReq.Id,
            StartUtc: dtp.UtcNow.AddMinutes(-10), // in the past
            DurationMinutes: 60,
            Mode: SessionMode.Online,
            RequireTeacherConfirmation: false
        );

        var result = await bookingService.BookSessionAsync(learner.Id, request);

        Assert.False(result.IsSuccess);
        Assert.Contains("must be in the future", result.Error);
    }

    [Fact]
    public async Task BookSession_InsufficientBalance_ThrowsException()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        // Learner has only 30 minutes available
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 30, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var request = new BookSessionRequest(
            SwapRequestId: swapReq.Id,
            StartUtc: dtp.UtcNow.AddDays(1),
            DurationMinutes: 60, // Requesting 60 with only 30 available
            Mode: SessionMode.Online,
            RequireTeacherConfirmation: false
        );

        await Assert.ThrowsAsync<InsufficientBalanceException>(() =>
            bookingService.BookSessionAsync(learner.Id, request));

        // Wallet untouched
        var wallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(30, wallet.AvailableMinutes);
        Assert.Equal(0, wallet.HeldMinutes);
    }

    [Fact]
    public async Task BookSession_TeacherDoesNotTeachSkill_Rejected()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        // Remove the teacher's UserSkill for this skill
        var teacherSkills = await context.UserSkills.Where(us => us.UserId == teacher.Id).ToListAsync();
        context.UserSkills.RemoveRange(teacherSkills);
        await context.SaveChangesAsync();

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var request = new BookSessionRequest(
            SwapRequestId: swapReq.Id,
            StartUtc: dtp.UtcNow.AddDays(1),
            DurationMinutes: 60,
            Mode: SessionMode.Online,
            RequireTeacherConfirmation: false
        );

        var result = await bookingService.BookSessionAsync(learner.Id, request);

        Assert.False(result.IsSuccess);
        Assert.Contains("does not teach skill", result.Error);
    }

    [Fact]
    public async Task BookSession_ParticipantMismatch_ThrowsUnauthorized()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        var thirdParty = await TestDbHelper.CreateUserAsync(context, "thirdparty_" + Guid.NewGuid().ToString("N")[..8]);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var request = new BookSessionRequest(
            SwapRequestId: swapReq.Id,
            StartUtc: dtp.UtcNow.AddDays(1),
            DurationMinutes: 60,
            Mode: SessionMode.Online,
            RequireTeacherConfirmation: false
        );

        // Third party trying to book a swap request they are not part of
        await Assert.ThrowsAsync<UnauthorizedSessionAccessException>(() =>
            bookingService.BookSessionAsync(thirdParty.Id, request));
    }

    [Fact]
    public async Task BookSession_LearnerOverlap_Rejected()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var slotStart = dtp.UtcNow.AddDays(2);

        // First booking: 12:00 to 13:00
        var first = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            SwapRequestId: swapReq.Id,
            StartUtc: slotStart,
            DurationMinutes: 60,
            Mode: SessionMode.Online,
            RequireTeacherConfirmation: false
        ));
        Assert.True(first.IsSuccess);

        // Second booking for same learner: overlapping 12:30 to 13:30
        var overlapRequest = new BookSessionRequest(
            SwapRequestId: swapReq.Id,
            StartUtc: slotStart.AddMinutes(30),
            DurationMinutes: 60,
            Mode: SessionMode.Online,
            RequireTeacherConfirmation: false
        );

        await Assert.ThrowsAsync<SessionOverlapException>(() =>
            bookingService.BookSessionAsync(learner.Id, overlapRequest));
    }

    [Fact]
    public async Task BookSession_TeacherOverlap_Rejected()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner1, teacher, skill, swapReq1) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        // Create second learner with swap request to the same teacher
        var learner2 = await TestDbHelper.CreateUserAsync(context, "lrn2_" + Guid.NewGuid().ToString("N")[..8]);
        var wallet2 = new Wallet { UserId = learner2.Id, AvailableMinutes = 180, HeldMinutes = 0, UpdatedAtUtc = dtp.UtcNow };
        context.Wallets.Add(wallet2);
        var swapReq2 = new SwapRequest
        {
            RequesterId = learner2.Id,
            ReceiverId = teacher.Id,
            SkillId = skill.Id,
            Status = SwapRequestStatus.Accepted,
            CreatedAtUtc = dtp.UtcNow.AddDays(-1),
            RespondedAtUtc = dtp.UtcNow.AddHours(-1)
        };
        context.SwapRequests.Add(swapReq2);
        await context.SaveChangesAsync();

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var slotStart = dtp.UtcNow.AddDays(3);

        // Learner 1 books teacher at 14:00 - 15:00
        var first = await bookingService.BookSessionAsync(learner1.Id, new BookSessionRequest(
            SwapRequestId: swapReq1.Id,
            StartUtc: slotStart,
            DurationMinutes: 60,
            Mode: SessionMode.Online,
            RequireTeacherConfirmation: false
        ));
        Assert.True(first.IsSuccess);

        // Learner 2 attempts to book teacher at overlapping 14:30 - 15:30
        var overlapRequest = new BookSessionRequest(
            SwapRequestId: swapReq2.Id,
            StartUtc: slotStart.AddMinutes(30),
            DurationMinutes: 60,
            Mode: SessionMode.Online,
            RequireTeacherConfirmation: false
        );

        var ex = await Assert.ThrowsAsync<SessionOverlapException>(() =>
            bookingService.BookSessionAsync(learner2.Id, overlapRequest));

        Assert.Contains("Teacher", ex.Message);
    }
    [Fact]
    public async Task BookSession_ReversedRoles_FailsWithUnauthorized()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 180);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        // Teacher (Receiver) attempts to book the session as if they were the learner
        var reversedRequest = new BookSessionRequest(
            SwapRequestId: swapReq.Id,
            StartUtc: dtp.UtcNow.AddDays(4),
            DurationMinutes: 60,
            Mode: SessionMode.Online,
            RequireTeacherConfirmation: false
        );

        await Assert.ThrowsAsync<UnauthorizedSessionAccessException>(() =>
            bookingService.BookSessionAsync(teacher.Id, reversedRequest));
    }

    [Fact]
    public async Task BookSession_WrongSkillId_FailsValidation()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        // Supply a mismatching SkillId
        var wrongSkillRequest = new BookSessionRequest(
            SwapRequestId: swapReq.Id,
            StartUtc: dtp.UtcNow.AddDays(4),
            DurationMinutes: 60,
            Mode: SessionMode.Online,
            RequireTeacherConfirmation: false,
            SkillId: skill.Id + 999
        );

        var result = await bookingService.BookSessionAsync(learner.Id, wrongSkillRequest);

        Assert.False(result.IsSuccess);
        Assert.Contains("does not match SwapRequest SkillId", result.Error);
    }

    [Fact]
    public async Task BookSession_SameUserAsTeacherAndLearner_RejectedByCheckConstraint()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        // Database-level check constraint CK_SwapRequest_Requester_Receiver prevents requester == receiver
        var invalidSwap = new SwapRequest
        {
            RequesterId = learner.Id,
            ReceiverId = learner.Id,
            SkillId = skill.Id,
            Status = SwapRequestStatus.Accepted,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            RespondedAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        context.SwapRequests.Add(invalidSwap);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("CK_SwapRequest_Requester_Receiver", ex.InnerException?.Message);
    }
}

