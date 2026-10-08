using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.DTOs.Sessions;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Domain.Exceptions;
using SkillSwap.Infrastructure.Services;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class NoShowHandlingTests
{
    [Fact]
    public async Task Teacher_ReportingLearnerNoShow_PaysTeacher_AndCapturesHeldMinutes()
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

        // Teacher legitimately reports learner as no-show
        var result = await sessionService.MarkNoShowAsync(b.Value.Id, teacher.Id, NoShowParty.Learner);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionStatus.NoShow, result.Value.Status);
        Assert.Equal(learner.Id, result.Value.NoShowUserId);

        // Learner wallet: Held = 0, Available = 120
        var learnerWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(120, learnerWallet.AvailableMinutes);
        Assert.Equal(0, learnerWallet.HeldMinutes);

        // Teacher wallet: Available = 60
        var teacherWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == teacher.Id);
        Assert.Equal(60, teacherWallet.AvailableMinutes);
        Assert.Equal(0, teacherWallet.HeldMinutes);

        // Verify Ledger: Capture for Learner, Earn for Teacher
        var captureTx = await context.CreditTransactions.FirstOrDefaultAsync(ct => ct.SessionId == b.Value.Id && ct.Type == CreditTransactionType.Capture);
        Assert.NotNull(captureTx);
        Assert.Equal(learner.Id, captureTx.WalletId);

        var earnTx = await context.CreditTransactions.FirstOrDefaultAsync(ct => ct.SessionId == b.Value.Id && ct.Type == CreditTransactionType.Earn);
        Assert.NotNull(earnTx);
        Assert.Equal(teacher.Id, earnTx.WalletId);
    }

    [Fact]
    public async Task Learner_ReportingTeacherNoShow_ReleasesLearnerHold_DoesNotPayTeacher()
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

        // Learner legitimately reports teacher as no-show
        var result = await sessionService.MarkNoShowAsync(b.Value.Id, learner.Id, NoShowParty.Teacher);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionStatus.NoShow, result.Value.Status);
        Assert.Equal(teacher.Id, result.Value.NoShowUserId);

        // Learner wallet: Held = 0, Available restored to 180
        var learnerWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(180, learnerWallet.AvailableMinutes);
        Assert.Equal(0, learnerWallet.HeldMinutes);

        // Teacher wallet: Still 0
        var teacherWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == teacher.Id);
        Assert.Equal(0, teacherWallet.AvailableMinutes);

        // Verify Release tx exists
        var releaseTx = await context.CreditTransactions.FirstOrDefaultAsync(ct => ct.SessionId == b.Value.Id && ct.Type == CreditTransactionType.Release);
        Assert.NotNull(releaseTx);
    }

    [Fact]
    public async Task Learner_CannotReportLearnerNoShow()
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

        // Learner cannot report Learner no-show
        await Assert.ThrowsAsync<DomainException>(() =>
            sessionService.MarkNoShowAsync(b.Value.Id, learner.Id, NoShowParty.Learner));
    }

    [Fact]
    public async Task Teacher_CannotReportTeacherNoShow()
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

        // Teacher cannot report Teacher no-show
        await Assert.ThrowsAsync<DomainException>(() =>
            sessionService.MarkNoShowAsync(b.Value.Id, teacher.Id, NoShowParty.Teacher));
    }

    [Fact]
    public async Task Participant_CannotReportBothNoShow()
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

        // Neither learner nor teacher can report Both
        await Assert.ThrowsAsync<DomainException>(() =>
            sessionService.MarkNoShowAsync(b.Value.Id, learner.Id, NoShowParty.Both));

        await Assert.ThrowsAsync<DomainException>(() =>
            sessionService.MarkNoShowAsync(b.Value.Id, teacher.Id, NoShowParty.Both));
    }

    [Fact]
    public async Task UnrelatedUser_CannotReportNoShow()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);
        var unrelated = await TestDbHelper.CreateUserAsync(context, "unrel_ns_" + Guid.NewGuid().ToString("N")[..8]);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var sessionService = new SessionService(context, dtp, walletLockService);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(1), 60, SessionMode.Online, null, false));
        Assert.True(b.IsSuccess);

        // Unrelated user cannot report no-show
        await Assert.ThrowsAsync<UnauthorizedSessionAccessException>(() =>
            sessionService.MarkNoShowAsync(b.Value.Id, unrelated.Id, NoShowParty.Learner));
    }

    [Fact]
    public async Task Admin_CanMarkBothNoShow_ReleasesLearnerHold()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 180, 0);
        var admin = await TestDbHelper.CreateUserAsync(context, "admin_ns_" + Guid.NewGuid().ToString("N")[..8]);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var sessionService = new SessionService(context, dtp, walletLockService);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(1), 60, SessionMode.Online, null, false));
        Assert.True(b.IsSuccess);

        // Admin marks Both
        var result = await sessionService.MarkNoShowAsync(b.Value.Id, admin.Id, NoShowParty.Both, isAdmin: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionStatus.NoShow, result.Value.Status);
        Assert.Null(result.Value.NoShowUserId);

        // Learner restored
        var learnerWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(180, learnerWallet.AvailableMinutes);
        Assert.Equal(0, learnerWallet.HeldMinutes);

        // Teacher receives 0
        var teacherWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == teacher.Id);
        Assert.Equal(0, teacherWallet.AvailableMinutes);
    }

    [Fact]
    public async Task DuplicateNoShow_IsIdempotent_DoesNotDuplicateTransfers()
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

        // First call
        var first = await sessionService.MarkNoShowAsync(b.Value.Id, teacher.Id, NoShowParty.Learner);
        Assert.True(first.IsSuccess);

        // Second call
        var second = await sessionService.MarkNoShowAsync(b.Value.Id, teacher.Id, NoShowParty.Learner);
        Assert.True(second.IsSuccess);

        // Teacher still has exactly 60 (not 120)
        var teacherWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == teacher.Id);
        Assert.Equal(60, teacherWallet.AvailableMinutes);

        var earnCount = await context.CreditTransactions.CountAsync(ct => ct.SessionId == b.Value.Id && ct.Type == CreditTransactionType.Earn);
        Assert.Equal(1, earnCount);
    }
}
