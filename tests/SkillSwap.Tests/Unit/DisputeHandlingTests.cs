using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.DTOs.Sessions;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Domain.Exceptions;
using SkillSwap.Infrastructure.Services;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class DisputeHandlingTests
{
    [Fact]
    public async Task ReportDispute_FreezesCredits_DoesNotTransferOrRelease()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var sessionService = new SessionService(context, dtp, walletLockService);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(1), 60, SessionMode.Online, null, false));
        Assert.True(b.IsSuccess);

        // Learner reports technical problem
        var disputeResult = await sessionService.ReportDisputeAsync(b.Value.Id, learner.Id, "Internet connection failed entirely");
        Assert.True(disputeResult.IsSuccess);
        Assert.Equal(SessionStatus.Disputed, disputeResult.Value.Status);
        Assert.Equal(learner.Id, disputeResult.Value.ReportedById);

        // Credits must remain frozen: Learner has 60 held, 120 available. Teacher has 0.
        var learnerWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(120, learnerWallet.AvailableMinutes);
        Assert.Equal(60, learnerWallet.HeldMinutes);

        var teacherWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == teacher.Id);
        Assert.Equal(0, teacherWallet.AvailableMinutes);

        // No Earn or Capture or Release transactions exist yet
        var txs = await context.CreditTransactions.Where(ct => ct.SessionId == b.Value.Id).ToListAsync();
        Assert.Single(txs); // only the initial Hold
        Assert.Equal(CreditTransactionType.Hold, txs[0].Type);
    }

    [Fact]
    public async Task ResolveDispute_AwardTeacher_TransfersCreditsToTeacher()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);
        var admin = await TestDbHelper.CreateUserAsync(context, "admin_" + Guid.NewGuid().ToString("N")[..8]);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var sessionService = new SessionService(context, dtp, walletLockService);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(1), 60, SessionMode.Online, null, false));
        Assert.True(b.IsSuccess);

        await sessionService.ReportDisputeAsync(b.Value.Id, learner.Id, "Dispute claim");

        // Admin rules in favor of teacher
        var resolveResult = await sessionService.ResolveDisputeAsync(b.Value.Id, admin.Id, awardTeacher: true, "Evidence shows teacher attended");
        Assert.True(resolveResult.IsSuccess);
        Assert.Equal(SessionStatus.Completed, resolveResult.Value.Status);

        var teacherWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == teacher.Id);
        Assert.Equal(60, teacherWallet.AvailableMinutes);

        var learnerWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(120, learnerWallet.AvailableMinutes);
        Assert.Equal(0, learnerWallet.HeldMinutes);
    }

    [Fact]
    public async Task ResolveDispute_RefundLearner_ReleasesHoldToLearner()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);
        var admin = await TestDbHelper.CreateUserAsync(context, "admin_" + Guid.NewGuid().ToString("N")[..8]);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var sessionService = new SessionService(context, dtp, walletLockService);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(1), 60, SessionMode.Online, null, false));
        Assert.True(b.IsSuccess);

        await sessionService.ReportDisputeAsync(b.Value.Id, learner.Id, "Dispute claim");

        // Admin rules in favor of learner
        var resolveResult = await sessionService.ResolveDisputeAsync(b.Value.Id, admin.Id, awardTeacher: false, "Technical fault confirmed on platform");
        Assert.True(resolveResult.IsSuccess);
        Assert.Equal(SessionStatus.Cancelled, resolveResult.Value.Status);

        var teacherWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == teacher.Id);
        Assert.Equal(0, teacherWallet.AvailableMinutes);

        var learnerWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(180, learnerWallet.AvailableMinutes);
        Assert.Equal(0, learnerWallet.HeldMinutes);
    }

    [Fact]
    public async Task ResolveDispute_ParticipantCannotSelfArbitrate()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var sessionService = new SessionService(context, dtp, walletLockService);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(1), 60, SessionMode.Online, null, false));
        Assert.True(b.IsSuccess);

        await sessionService.ReportDisputeAsync(b.Value.Id, learner.Id, "Dispute claim");

        // Teacher attempting to resolve as admin is rejected
        await Assert.ThrowsAsync<UnauthorizedSessionAccessException>(() =>
            sessionService.ResolveDisputeAsync(b.Value.Id, teacher.Id, awardTeacher: true, "Self resolution"));

        // Learner attempting to resolve as admin is rejected
        await Assert.ThrowsAsync<UnauthorizedSessionAccessException>(() =>
            sessionService.ResolveDisputeAsync(b.Value.Id, learner.Id, awardTeacher: false, "Self resolution"));
    }

    [Fact]
    public async Task ResolveDispute_EmptyAdminId_ThrowsUnauthorized()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var sessionService = new SessionService(context, dtp, walletLockService);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(1), 60, SessionMode.Online, null, false));
        Assert.True(b.IsSuccess);

        await sessionService.ReportDisputeAsync(b.Value.Id, learner.Id, "Dispute claim");

        // Empty admin id is rejected
        await Assert.ThrowsAsync<UnauthorizedSessionAccessException>(() =>
            sessionService.ResolveDisputeAsync(b.Value.Id, Guid.Empty, awardTeacher: true, "Invalid"));
    }
}
