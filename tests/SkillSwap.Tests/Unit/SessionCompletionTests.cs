using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkillSwap.Application.Common.Options;
using SkillSwap.Application.DTOs.Sessions;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Domain.Exceptions;
using SkillSwap.Infrastructure.Services;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class SessionCompletionTests
{
    [Fact]
    public async Task Teacher_CanCompleteSession_TransfersMinutes1to1()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(1), 60, SessionMode.Online, null, false));
        Assert.True(b.IsSuccess);

        var options = Options.Create(new SessionPolicyOptions());
        var completionService = new SessionCompletionService(context, dtp, walletLockService, options, NullLogger<SessionCompletionService>.Instance);

        // Teacher completes
        var result = await completionService.CompleteSessionAsync(b.Value.Id, teacher.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionStatus.Completed, result.Value.Status);

        // Verify Learner wallet: Available = 120, Held = 0
        var learnerWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(120, learnerWallet.AvailableMinutes);
        Assert.Equal(0, learnerWallet.HeldMinutes);

        // Verify Teacher wallet: Available = 60, Held = 0
        var teacherWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == teacher.Id);
        Assert.Equal(60, teacherWallet.AvailableMinutes);
        Assert.Equal(0, teacherWallet.HeldMinutes);

        // Verify Ledger: Capture for Learner, Earn for Teacher
        var captureTx = await context.CreditTransactions.FirstOrDefaultAsync(ct => ct.SessionId == b.Value.Id && ct.Type == CreditTransactionType.Capture);
        Assert.NotNull(captureTx);
        Assert.Equal(learner.Id, captureTx.WalletId);
        Assert.Equal(0, captureTx.AvailableDelta);
        Assert.Equal(-60, captureTx.HeldDelta);

        var earnTx = await context.CreditTransactions.FirstOrDefaultAsync(ct => ct.SessionId == b.Value.Id && ct.Type == CreditTransactionType.Earn);
        Assert.NotNull(earnTx);
        Assert.Equal(teacher.Id, earnTx.WalletId);
        Assert.Equal(60, earnTx.AvailableDelta);
        Assert.Equal(0, earnTx.HeldDelta);
    }

    [Fact]
    public async Task Learner_CanCompleteSession_TransfersMinutes1to1()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(1), 60, SessionMode.Online, null, false));
        Assert.True(b.IsSuccess);

        var options = Options.Create(new SessionPolicyOptions());
        var completionService = new SessionCompletionService(context, dtp, walletLockService, options, NullLogger<SessionCompletionService>.Instance);

        // Learner completes
        var result = await completionService.CompleteSessionAsync(b.Value.Id, learner.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionStatus.Completed, result.Value.Status);

        var teacherWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == teacher.Id);
        Assert.Equal(60, teacherWallet.AvailableMinutes);
    }

    [Fact]
    public async Task UnrelatedUser_CannotCompleteSession_ThrowsUnauthorized()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);
        var unrelatedUser = await TestDbHelper.CreateUserAsync(context, "unrelated_" + Guid.NewGuid().ToString("N")[..8]);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(1), 60, SessionMode.Online, null, false));
        Assert.True(b.IsSuccess);

        var options = Options.Create(new SessionPolicyOptions());
        var completionService = new SessionCompletionService(context, dtp, walletLockService, options, NullLogger<SessionCompletionService>.Instance);

        // Unrelated user tries to complete
        await Assert.ThrowsAsync<UnauthorizedSessionAccessException>(() =>
            completionService.CompleteSessionAsync(b.Value.Id, unrelatedUser.Id));

        // Session must remain Scheduled
        var session = await context.Sessions.AsNoTracking().FirstAsync(s => s.Id == b.Value.Id);
        Assert.Equal(SessionStatus.Scheduled, session.Status);

        // Teacher receives no credits
        var teacherWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == teacher.Id);
        Assert.Equal(0, teacherWallet.AvailableMinutes);

        // Learner still has 60 held
        var learnerWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(60, learnerWallet.HeldMinutes);
    }

    [Fact]
    public async Task DoubleCompletion_IsIdempotent_DoesNotDoublePayTeacher()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(1), 60, SessionMode.Online, null, false));
        Assert.True(b.IsSuccess);

        var options = Options.Create(new SessionPolicyOptions());
        var completionService = new SessionCompletionService(context, dtp, walletLockService, options, NullLogger<SessionCompletionService>.Instance);

        // Call Complete first time by Teacher
        var first = await completionService.CompleteSessionAsync(b.Value.Id, teacher.Id);
        Assert.True(first.IsSuccess);

        // Call Complete second time by Learner
        var second = await completionService.CompleteSessionAsync(b.Value.Id, learner.Id);
        Assert.True(second.IsSuccess);
        Assert.Equal(SessionStatus.Completed, second.Value.Status);

        // Teacher balance must remain 60 (NOT 120)
        var teacherWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == teacher.Id);
        Assert.Equal(60, teacherWallet.AvailableMinutes);

        // Exactly ONE Earn transaction and ONE Capture transaction exist
        var earnCount = await context.CreditTransactions.CountAsync(ct => ct.SessionId == b.Value.Id && ct.Type == CreditTransactionType.Earn);
        var captureCount = await context.CreditTransactions.CountAsync(ct => ct.SessionId == b.Value.Id && ct.Type == CreditTransactionType.Capture);
        Assert.Equal(1, earnCount);
        Assert.Equal(1, captureCount);
    }

    [Fact]
    public async Task BackgroundAutoComplete_WorksIndependently_AndIgnoresDisputes()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 300, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        // Session 1: Eligible for auto-complete (Scheduled, EndUtc in the past beyond grace period)
        var startPast = dtp.UtcNow.AddHours(-2);
        var session1 = new Session
        {
            SwapRequestId = swapReq.Id,
            TeacherId = teacher.Id,
            LearnerId = learner.Id,
            SkillId = skill.Id,
            StartUtc = startPast,
            EndUtc = startPast.AddMinutes(60),
            DurationMinutes = 60,
            Mode = SessionMode.Online,
            Status = SessionStatus.Scheduled,
            CreatedAtUtc = dtp.UtcNow.AddHours(-3)
        };
        context.Sessions.Add(session1);

        // Set learner wallet hold for session 1
        var learnerWallet = await context.Wallets.FirstAsync(w => w.UserId == learner.Id);
        learnerWallet.AvailableMinutes -= 60;
        learnerWallet.HeldMinutes += 60;

        // Session 2: Disputed session (EndUtc in the past, but Status = Disputed)
        var session2 = new Session
        {
            SwapRequestId = swapReq.Id,
            TeacherId = teacher.Id,
            LearnerId = learner.Id,
            SkillId = skill.Id,
            StartUtc = startPast,
            EndUtc = startPast.AddMinutes(60),
            DurationMinutes = 60,
            Mode = SessionMode.Online,
            Status = SessionStatus.Disputed,
            ReportedById = learner.Id,
            ResolutionNote = "Audio did not work",
            CreatedAtUtc = dtp.UtcNow.AddHours(-3)
        };
        context.Sessions.Add(session2);
        learnerWallet.AvailableMinutes -= 60;
        learnerWallet.HeldMinutes += 60;

        await context.SaveChangesAsync();

        var options = Options.Create(new SessionPolicyOptions { AutoCompletionGracePeriodMinutes = 15 });
        var completionService = new SessionCompletionService(context, dtp, walletLockService, options, NullLogger<SessionCompletionService>.Instance);

        var completedCount = await completionService.AutoCompleteEligibleSessionsAsync();

        Assert.True(completedCount >= 1);

        // Check Session 1 is Completed
        var s1 = await context.Sessions.AsNoTracking().FirstAsync(s => s.Id == session1.Id);
        Assert.Equal(SessionStatus.Completed, s1.Status);

        // Check Session 2 remains Disputed
        var s2 = await context.Sessions.AsNoTracking().FirstAsync(s => s.Id == session2.Id);
        Assert.Equal(SessionStatus.Disputed, s2.Status);
    }
}
