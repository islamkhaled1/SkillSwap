using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkillSwap.Application.Common.Options;
using SkillSwap.Application.DTOs.Sessions;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Services;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class SessionExpirationTests
{
    [Fact]
    public async Task PendingSession_ExpiresAfter24Hours_WhenStartUtcInFuture()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        // Book with RequireTeacherConfirmation = true, StartUtc is 5 days away
        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(5), 60, SessionMode.Online, null, true));
        Assert.True(b.IsSuccess);
        Assert.Equal(SessionStatus.PendingConfirmation, b.Value.Status);

        // Simulate created 25 hours ago (> 24 hours threshold)
        await context.Sessions
            .Where(s => s.Id == b.Value.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, dtp.UtcNow.AddHours(-25)));

        var options = Options.Create(new SessionPolicyOptions { PendingConfirmationHours = 24 });
        var completionService = new SessionCompletionService(context, dtp, walletLockService, options, NullLogger<SessionCompletionService>.Instance);

        var expiredCount = await completionService.ExpirePendingSessionsAsync();

        Assert.True(expiredCount >= 1);

        // Session status is Expired
        var session = await context.Sessions.AsNoTracking().FirstAsync(s => s.Id == b.Value.Id);
        Assert.Equal(SessionStatus.Expired, session.Status);

        // Learner wallet restored to 180 available, 0 held
        var learnerWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(180, learnerWallet.AvailableMinutes);
        Assert.Equal(0, learnerWallet.HeldMinutes);

        // Exactly one release transaction exists
        var releaseCount = await context.CreditTransactions.CountAsync(ct => ct.SessionId == b.Value.Id && ct.Type == CreditTransactionType.Release);
        Assert.Equal(1, releaseCount);
    }

    [Fact]
    public async Task PendingSession_ExpiresWhenStartUtcArrives_Before24Hours()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        // Book with RequireTeacherConfirmation = true, StartUtc is 2 hours away
        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddHours(2), 60, SessionMode.Online, null, true));
        Assert.True(b.IsSuccess);
        Assert.Equal(SessionStatus.PendingConfirmation, b.Value.Status);

        // Simulate that 3 hours have passed:
        // CreatedAtUtc is now 3 hours ago (< 24h limit), but StartUtc was 1 hour ago (StartUtc <= now)
        await context.Sessions
            .Where(s => s.Id == b.Value.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.CreatedAtUtc, dtp.UtcNow.AddHours(-3))
                .SetProperty(x => x.StartUtc, dtp.UtcNow.AddHours(-1))
                .SetProperty(x => x.EndUtc, dtp.UtcNow));

        var options = Options.Create(new SessionPolicyOptions { PendingConfirmationHours = 24 });
        var completionService = new SessionCompletionService(context, dtp, walletLockService, options, NullLogger<SessionCompletionService>.Instance);

        var expiredCount = await completionService.ExpirePendingSessionsAsync();

        Assert.True(expiredCount >= 1);

        // Session status is Expired because StartUtc arrived before confirmation
        var session = await context.Sessions.AsNoTracking().FirstAsync(s => s.Id == b.Value.Id);
        Assert.Equal(SessionStatus.Expired, session.Status);

        // Learner wallet restored
        var learnerWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(180, learnerWallet.AvailableMinutes);
        Assert.Equal(0, learnerWallet.HeldMinutes);

        // Quota is not consumed for expired session
        var usedQuota = await quotaService.GetUsedLearningMinutesAsync(learner.Id, dtp.UtcNow);
        Assert.Equal(0, usedQuota);
    }

    [Fact]
    public async Task RepeatedExpiration_DoesNotDoubleReleaseMinutes()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(2), 60, SessionMode.Online, null, true));
        Assert.True(b.IsSuccess);

        await context.Sessions
            .Where(s => s.Id == b.Value.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, dtp.UtcNow.AddHours(-25)));

        var options = Options.Create(new SessionPolicyOptions { PendingConfirmationHours = 24 });
        var completionService = new SessionCompletionService(context, dtp, walletLockService, options, NullLogger<SessionCompletionService>.Instance);

        // First run
        var count1 = await completionService.ExpirePendingSessionsAsync();
        Assert.True(count1 >= 1);

        // Second run
        var count2 = await completionService.ExpirePendingSessionsAsync();
        Assert.Equal(0, count2);

        // Third run on single session directly
        var singleResult = await completionService.ExpirePendingSessionAsync(b.Value.Id);
        Assert.True(singleResult.IsSuccess);
        Assert.Equal(SessionStatus.Expired, singleResult.Value.Status);

        // Wallet balance remains 180 (never double refunded to 240)
        var learnerWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(180, learnerWallet.AvailableMinutes);
        Assert.Equal(0, learnerWallet.HeldMinutes);

        // Exactly 1 release transaction
        var releaseCount = await context.CreditTransactions.CountAsync(ct => ct.SessionId == b.Value.Id && ct.Type == CreditTransactionType.Release);
        Assert.Equal(1, releaseCount);
    }
}
