using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.DTOs.Sessions;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Domain.Exceptions;
using SkillSwap.Infrastructure.Services;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class SessionCancellationTests
{
    [Fact]
    public async Task LearnerCancellation_ReleasesHold_AndMarksSessionCancelled()
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

        // Cancel by learner
        var cancelResult = await sessionService.CancelSessionAsync(b.Value.Id, learner.Id, "Learner conflict");
        Assert.True(cancelResult.IsSuccess);
        Assert.Equal(SessionStatus.Cancelled, cancelResult.Value.Status);
        Assert.Equal(learner.Id, cancelResult.Value.CancelledById);

        // Verify wallet restored
        var wallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(180, wallet.AvailableMinutes);
        Assert.Equal(0, wallet.HeldMinutes);

        // Verify Release transaction exists
        var releaseTx = await context.CreditTransactions.FirstOrDefaultAsync(ct => ct.SessionId == b.Value.Id && ct.Type == CreditTransactionType.Release);
        Assert.NotNull(releaseTx);
        Assert.Equal(60, releaseTx.AvailableDelta);
        Assert.Equal(-60, releaseTx.HeldDelta);
    }

    [Fact]
    public async Task TeacherCancellation_ReleasesHold_AndMarksSessionCancelled()
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

        // Cancel by teacher
        var cancelResult = await sessionService.CancelSessionAsync(b.Value.Id, teacher.Id, "Teacher emergency");
        Assert.True(cancelResult.IsSuccess);
        Assert.Equal(SessionStatus.Cancelled, cancelResult.Value.Status);
        Assert.Equal(teacher.Id, cancelResult.Value.CancelledById);

        // Verify wallet restored
        var wallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(180, wallet.AvailableMinutes);
        Assert.Equal(0, wallet.HeldMinutes);
    }

    [Fact]
    public async Task DuplicateCancellation_IsIdempotent_DoesNotDoubleRelease()
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

        // Cancel first time
        var first = await sessionService.CancelSessionAsync(b.Value.Id, learner.Id, "Cancel 1");
        Assert.True(first.IsSuccess);

        // Cancel second time
        var second = await sessionService.CancelSessionAsync(b.Value.Id, learner.Id, "Cancel 2");
        Assert.True(second.IsSuccess);
        Assert.Equal(SessionStatus.Cancelled, second.Value.Status);

        // Ensure wallet balance is still exactly 180 (not double refunded to 240)
        var wallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(180, wallet.AvailableMinutes);
        Assert.Equal(0, wallet.HeldMinutes);

        // Ensure exactly ONE release transaction exists
        var releaseCount = await context.CreditTransactions
            .CountAsync(ct => ct.SessionId == b.Value.Id && ct.Type == CreditTransactionType.Release);
        Assert.Equal(1, releaseCount);
    }

    [Fact]
    public async Task UnauthorizedUser_CannotCancelSession()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);
        var randomUser = await TestDbHelper.CreateUserAsync(context, "rando_" + Guid.NewGuid().ToString("N")[..8]);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var sessionService = new SessionService(context, dtp, walletLockService);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(1), 60, SessionMode.Online, null, false));
        Assert.True(b.IsSuccess);

        await Assert.ThrowsAsync<UnauthorizedSessionAccessException>(() =>
            sessionService.CancelSessionAsync(b.Value.Id, randomUser.Id));
    }
}
