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

namespace SkillSwap.Tests.Integration;

public class SqlServerConcurrencyTests
{
    [Fact]
    public async Task ConcurrentBooking_CannotDoubleSpendSameWallet()
    {
        using var setupContext = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();

        // Learner has ONLY 60 minutes available
        var (learner, teacher1, skill1, swapReq1) = await TestDbHelper.CreateStandardBookingSetupAsync(setupContext, 60, 0);

        // Teacher 2 with skill 1
        var teacher2 = await TestDbHelper.CreateUserAsync(setupContext, "tch2_" + Guid.NewGuid().ToString("N")[..8]);
        var teacherWallet2 = new Wallet { UserId = teacher2.Id, AvailableMinutes = 0, HeldMinutes = 0, UpdatedAtUtc = dtp.UtcNow };
        setupContext.Wallets.Add(teacherWallet2);
        var teacherSkill2 = new UserSkill
        {
            UserId = teacher2.Id,
            SkillId = skill1.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Advanced,
            CreatedAtUtc = dtp.UtcNow
        };
        setupContext.UserSkills.Add(teacherSkill2);

        var swapReq2 = new SwapRequest
        {
            RequesterId = learner.Id,
            ReceiverId = teacher2.Id,
            SkillId = skill1.Id,
            Status = SwapRequestStatus.Accepted,
            CreatedAtUtc = dtp.UtcNow.AddDays(-1),
            RespondedAtUtc = dtp.UtcNow.AddHours(-1)
        };
        setupContext.SwapRequests.Add(swapReq2);
        await setupContext.SaveChangesAsync();

        var startSlot1 = dtp.UtcNow.AddDays(10);
        var startSlot2 = dtp.UtcNow.AddDays(11);

        var task1 = Task.Run(async () =>
        {
            using var context = TestDbContextFactory.CreateSqlServerContext();
            var walletLockService = new SqlWalletLockService(context, dtp);
            var quotaService = new QuotaService(context);
            var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

            try
            {
                var res = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
                    swapReq1.Id, startSlot1, 60, SessionMode.Online, null, false));
                return (Success: res.IsSuccess, Error: res.Error);
            }
            catch (Exception ex)
            {
                return (Success: false, Error: ex.Message);
            }
        });

        var task2 = Task.Run(async () =>
        {
            using var context = TestDbContextFactory.CreateSqlServerContext();
            var walletLockService = new SqlWalletLockService(context, dtp);
            var quotaService = new QuotaService(context);
            var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

            try
            {
                var res = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
                    swapReq2.Id, startSlot2, 60, SessionMode.Online, null, false));
                return (Success: res.IsSuccess, Error: res.Error);
            }
            catch (Exception ex)
            {
                return (Success: false, Error: ex.Message);
            }
        });

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);

        // Exactly one should succeed, and exactly one should fail due to insufficient balance
        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        using var verifyContext = TestDbContextFactory.CreateSqlServerContext();
        var wallet = await verifyContext.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(0, wallet.AvailableMinutes);
        Assert.Equal(60, wallet.HeldMinutes);
        Assert.True(wallet.AvailableMinutes >= 0, "AvailableMinutes must never become negative!");
    }

    [Fact]
    public async Task ConcurrentBooking_CannotCreateOverlappingSessionForSameTeacher()
    {
        using var setupContext = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();

        var (learner1, teacher, skill, swapReq1) = await TestDbHelper.CreateStandardBookingSetupAsync(setupContext, 180, 0);

        var learner2 = await TestDbHelper.CreateUserAsync(setupContext, "lrn2_" + Guid.NewGuid().ToString("N")[..8]);
        var learnerWallet2 = new Wallet { UserId = learner2.Id, AvailableMinutes = 180, HeldMinutes = 0, UpdatedAtUtc = dtp.UtcNow };
        setupContext.Wallets.Add(learnerWallet2);

        var swapReq2 = new SwapRequest
        {
            RequesterId = learner2.Id,
            ReceiverId = teacher.Id,
            SkillId = skill.Id,
            Status = SwapRequestStatus.Accepted,
            CreatedAtUtc = dtp.UtcNow.AddDays(-1),
            RespondedAtUtc = dtp.UtcNow.AddHours(-1)
        };
        setupContext.SwapRequests.Add(swapReq2);
        await setupContext.SaveChangesAsync();

        var identicalSlot = dtp.UtcNow.AddDays(12);

        var task1 = Task.Run(async () =>
        {
            using var context = TestDbContextFactory.CreateSqlServerContext();
            var walletLockService = new SqlWalletLockService(context, dtp);
            var quotaService = new QuotaService(context);
            var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

            try
            {
                var res = await bookingService.BookSessionAsync(learner1.Id, new BookSessionRequest(
                    swapReq1.Id, identicalSlot, 60, SessionMode.Online, null, false));
                return (Success: res.IsSuccess, Error: res.Error);
            }
            catch (Exception ex)
            {
                return (Success: false, Error: ex.Message);
            }
        });

        var task2 = Task.Run(async () =>
        {
            using var context = TestDbContextFactory.CreateSqlServerContext();
            var walletLockService = new SqlWalletLockService(context, dtp);
            var quotaService = new QuotaService(context);
            var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

            try
            {
                var res = await bookingService.BookSessionAsync(learner2.Id, new BookSessionRequest(
                    swapReq2.Id, identicalSlot, 60, SessionMode.Online, null, false));
                return (Success: res.IsSuccess, Error: res.Error);
            }
            catch (Exception ex)
            {
                return (Success: false, Error: ex.Message);
            }
        });

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);

        // Exactly one succeeds, the other fails due to overlap
        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        using var verifyContext = TestDbContextFactory.CreateSqlServerContext();
        var sessions = await verifyContext.Sessions
            .Where(s => s.TeacherId == teacher.Id && s.StartUtc == identicalSlot && s.Status == SessionStatus.Scheduled)
            .ToListAsync();
        Assert.Single(sessions);
    }

    [Fact]
    public async Task ConcurrentBooking_MonthlyQuotaEnforced_UnderParallelRequests()
    {
        using var setupContext = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();

        // Learner has Free Plan (180m cap) and 300m available in wallet
        var (learner, teacher1, skill, swapReq1) = await TestDbHelper.CreateStandardBookingSetupAsync(setupContext, 300, 0);

        // Create second teacher with accepted swap request for different slot
        var teacher2 = await TestDbHelper.CreateUserAsync(setupContext, "tch2_q_" + Guid.NewGuid().ToString("N")[..8]);
        var teacherWallet2 = new Wallet { UserId = teacher2.Id, AvailableMinutes = 0, HeldMinutes = 0, UpdatedAtUtc = dtp.UtcNow };
        setupContext.Wallets.Add(teacherWallet2);
        var teacherSkill2 = new UserSkill
        {
            UserId = teacher2.Id,
            SkillId = skill.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Intermediate,
            CreatedAtUtc = dtp.UtcNow
        };
        setupContext.UserSkills.Add(teacherSkill2);

        var swapReq2 = new SwapRequest
        {
            RequesterId = learner.Id,
            ReceiverId = teacher2.Id,
            SkillId = skill.Id,
            Status = SwapRequestStatus.Accepted,
            CreatedAtUtc = dtp.UtcNow.AddDays(-1),
            RespondedAtUtc = dtp.UtcNow.AddHours(-1)
        };
        setupContext.SwapRequests.Add(swapReq2);

        // Establish current usage = 120 minutes in target month (e.g. 2 scheduled sessions of 60m earlier in the month)
        var targetMonthSlot = dtp.UtcNow.AddDays(7);
        var existingSession1 = new Session
        {
            SwapRequestId = swapReq1.Id,
            TeacherId = teacher1.Id,
            LearnerId = learner.Id,
            SkillId = skill.Id,
            StartUtc = targetMonthSlot.AddDays(-2),
            EndUtc = targetMonthSlot.AddDays(-2).AddMinutes(60),
            DurationMinutes = 60,
            Mode = SessionMode.Online,
            Status = SessionStatus.Completed,
            CreatedAtUtc = dtp.UtcNow.AddDays(-3)
        };
        var existingSession2 = new Session
        {
            SwapRequestId = swapReq1.Id,
            TeacherId = teacher1.Id,
            LearnerId = learner.Id,
            SkillId = skill.Id,
            StartUtc = targetMonthSlot.AddDays(-1),
            EndUtc = targetMonthSlot.AddDays(-1).AddMinutes(60),
            DurationMinutes = 60,
            Mode = SessionMode.Online,
            Status = SessionStatus.Completed,
            CreatedAtUtc = dtp.UtcNow.AddDays(-3)
        };
        setupContext.Sessions.AddRange(existingSession1, existingSession2);
        await setupContext.SaveChangesAsync();

        // Verify initial quota usage is exactly 120 minutes
        var qService = new QuotaService(setupContext);
        var initialUsage = await qService.GetUsedLearningMinutesAsync(learner.Id, targetMonthSlot);
        Assert.Equal(120, initialUsage);

        // Two concurrent tasks each attempt a 60-minute booking in that target month
        var slot1 = targetMonthSlot.AddHours(2);
        var slot2 = targetMonthSlot.AddHours(4);

        var task1 = Task.Run(async () =>
        {
            using var context = TestDbContextFactory.CreateSqlServerContext();
            var walletLockService = new SqlWalletLockService(context, dtp);
            var quotaService = new QuotaService(context);
            var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

            try
            {
                var res = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
                    swapReq1.Id, slot1, 60, SessionMode.Online, null, false));
                return (Success: res.IsSuccess, Error: res.Error, ExceptionType: (Type?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, Error: ex.Message, ExceptionType: ex.GetType());
            }
        });

        var task2 = Task.Run(async () =>
        {
            using var context = TestDbContextFactory.CreateSqlServerContext();
            var walletLockService = new SqlWalletLockService(context, dtp);
            var quotaService = new QuotaService(context);
            var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);

            try
            {
                var res = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
                    swapReq2.Id, slot2, 60, SessionMode.Online, null, false));
                return (Success: res.IsSuccess, Error: res.Error, ExceptionType: (Type?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, Error: ex.Message, ExceptionType: ex.GetType());
            }
        });

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);

        // Exactly ONE succeeds, the other fails with QuotaExceededException
        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        var failedResult = results.First(r => !r.Success);
        Assert.Equal(typeof(QuotaExceededException), failedResult.ExceptionType);

        using var verifyContext = TestDbContextFactory.CreateSqlServerContext();

        // Final quota usage must be exactly 180 minutes (120 + 60)
        var finalQuotaService = new QuotaService(verifyContext);
        var finalUsage = await finalQuotaService.GetUsedLearningMinutesAsync(learner.Id, targetMonthSlot);
        Assert.Equal(180, finalUsage);

        // Final wallet balance: Initial 300 - 60 held = 240 Available, 60 Held
        var wallet = await verifyContext.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learner.Id);
        Assert.Equal(240, wallet.AvailableMinutes);
        Assert.Equal(60, wallet.HeldMinutes);

        // Exactly ONE new Hold ledger entry was created
        var holdTxs = await verifyContext.CreditTransactions
            .Where(ct => ct.WalletId == learner.Id && ct.Type == CreditTransactionType.Hold)
            .ToListAsync();
        Assert.Single(holdTxs);
    }

    [Fact]
    public async Task ConcurrentCompletion_DoesNotDoublePayTeacher()
    {
        using var setupContext = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();

        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(setupContext, 180, 0);

        var walletLockService = new SqlWalletLockService(setupContext, dtp);
        var quotaService = new QuotaService(setupContext);
        var bookingService = new SessionBookingService(setupContext, dtp, walletLockService, quotaService);

        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(15), 60, SessionMode.Online, null, false));
        Assert.True(b.IsSuccess);

        var sessionId = b.Value.Id;

        var task1 = Task.Run(async () =>
        {
            using var context = TestDbContextFactory.CreateSqlServerContext();
            var lockService = new SqlWalletLockService(context, dtp);
            var options = Options.Create(new SessionPolicyOptions());
            var completionService = new SessionCompletionService(context, dtp, lockService, options, NullLogger<SessionCompletionService>.Instance);
            return await completionService.CompleteSessionAsync(sessionId, teacher.Id);
        });

        var task2 = Task.Run(async () =>
        {
            using var context = TestDbContextFactory.CreateSqlServerContext();
            var lockService = new SqlWalletLockService(context, dtp);
            var options = Options.Create(new SessionPolicyOptions());
            var completionService = new SessionCompletionService(context, dtp, lockService, options, NullLogger<SessionCompletionService>.Instance);
            return await completionService.CompleteSessionAsync(sessionId, learner.Id);
        });

        var results = await Task.WhenAll(task1, task2);

        Assert.True(results[0].IsSuccess);
        Assert.True(results[1].IsSuccess);

        using var verifyContext = TestDbContextFactory.CreateSqlServerContext();
        var teacherWallet = await verifyContext.Wallets.AsNoTracking().FirstAsync(w => w.UserId == teacher.Id);
        // Teacher must be paid exactly 60, NEVER 120
        Assert.Equal(60, teacherWallet.AvailableMinutes);

        var earnCount = await verifyContext.CreditTransactions
            .CountAsync(ct => ct.SessionId == sessionId && ct.Type == CreditTransactionType.Earn);
        Assert.Equal(1, earnCount);
    }
}
