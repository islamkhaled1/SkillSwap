using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.DTOs.Sessions;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Domain.Exceptions;
using SkillSwap.Infrastructure.Services;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class MonthlyQuotaTests
{
    [Fact]
    public async Task FreeTier_MonthlyLimit_Is180Minutes()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var user = await TestDbHelper.CreateUserAsync(context, "free_user_" + Guid.NewGuid().ToString("N")[..8], "Free");
        var quotaService = new QuotaService(context);

        var limit = await quotaService.GetMonthlyLimitMinutesAsync(user.Id, DateTime.UtcNow);

        Assert.Equal(180, limit);
    }

    [Fact]
    public async Task PremiumTier_MonthlyLimit_Is720Minutes()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var user = await TestDbHelper.CreateUserAsync(context, "prem_user_" + Guid.NewGuid().ToString("N")[..8], "Premium");
        var quotaService = new QuotaService(context);

        var limit = await quotaService.GetMonthlyLimitMinutesAsync(user.Id, DateTime.UtcNow);

        Assert.Equal(720, limit);
    }

    [Fact]
    public async Task MonthlyQuota_PreventsBooking_WhenLimitExceeded()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        // Create learner on Free plan (180 min limit) with 300 minutes available in wallet
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 300, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var startUtc = dtp.UtcNow.AddDays(1);

        // Book 90 minutes
        var b1 = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, startUtc, 90, SessionMode.Online, null, false));
        Assert.True(b1.IsSuccess);

        // Book another 90 minutes (total 180 = exact limit)
        var b2 = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, startUtc.AddHours(2), 90, SessionMode.Online, null, false));
        Assert.True(b2.IsSuccess);

        // Third booking of 30 minutes exceeds 180 quota (180 + 30 > 180) -> throws QuotaExceededException
        await Assert.ThrowsAsync<QuotaExceededException>(() =>
            bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
                swapReq.Id, startUtc.AddHours(4), 30, SessionMode.Online, null, false)));
    }

    [Fact]
    public async Task CancelledAndExpiredSessions_DoNotConsumeQuota()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 300, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var sessionService = new SessionService(context, dtp, walletLockService);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var startUtc = dtp.UtcNow.AddDays(2);

        // Book 60m
        var b1 = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, startUtc, 60, SessionMode.Online, null, false));
        Assert.True(b1.IsSuccess);

        var usedBeforeCancel = await quotaService.GetUsedLearningMinutesAsync(learner.Id, startUtc);
        Assert.Equal(60, usedBeforeCancel);

        // Cancel the session
        var cancelResult = await sessionService.CancelSessionAsync(b1.Value.Id, learner.Id, "Cancelling session");
        Assert.True(cancelResult.IsSuccess);

        // Used quota is freed back to 0
        var usedAfterCancel = await quotaService.GetUsedLearningMinutesAsync(learner.Id, startUtc);
        Assert.Equal(0, usedAfterCancel);
    }

    [Fact]
    public async Task LearnerNoShow_KeepsQuotaConsumed()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 300, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var sessionService = new SessionService(context, dtp, walletLockService);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var startUtc = dtp.UtcNow.AddDays(3);

        var b1 = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, startUtc, 60, SessionMode.Online, null, false));
        Assert.True(b1.IsSuccess);

        // Mark Learner No-Show
        var noShowResult = await sessionService.MarkNoShowAsync(b1.Value.Id, teacher.Id, NoShowParty.Learner);
        Assert.True(noShowResult.IsSuccess);

        // Learner quota remains consumed
        var usedQuota = await quotaService.GetUsedLearningMinutesAsync(learner.Id, startUtc);
        Assert.Equal(60, usedQuota);
    }

    [Fact]
    public async Task TeacherNoShow_ReleasesLearnerQuota()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 300, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var sessionService = new SessionService(context, dtp, walletLockService);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var startUtc = dtp.UtcNow.AddDays(4);

        var b1 = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, startUtc, 60, SessionMode.Online, null, false));
        Assert.True(b1.IsSuccess);

        // Mark Teacher No-Show
        var noShowResult = await sessionService.MarkNoShowAsync(b1.Value.Id, learner.Id, NoShowParty.Teacher);
        Assert.True(noShowResult.IsSuccess);

        // Learner quota is released (0 consumed)
        var usedQuota = await quotaService.GetUsedLearningMinutesAsync(learner.Id, startUtc);
        Assert.Equal(0, usedQuota);
    }

    [Fact]
    public async Task TeachingSessions_DoNotCountTowardsQuota()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 300, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var startUtc = dtp.UtcNow.AddDays(5);

        // Learner books session with teacher
        var b1 = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, startUtc, 60, SessionMode.Online, null, false));
        Assert.True(b1.IsSuccess);

        // Teacher's used learning quota in that month is still 0 (teaching is uncapped)
        var teacherQuota = await quotaService.GetUsedLearningMinutesAsync(teacher.Id, startUtc);
        Assert.Equal(0, teacherQuota);
    }
}

