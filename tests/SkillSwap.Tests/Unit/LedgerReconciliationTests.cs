using SkillSwap.Application.DTOs.Sessions;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Services;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class LedgerReconciliationTests
{
    [Fact]
    public async Task ReconcileWallet_DetectsPerfectLedgerConsistency()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var (learner, teacher, skill, swapReq) = await TestDbHelper.CreateStandardBookingSetupAsync(context, 0, 0);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var quotaService = new QuotaService(context);
        var bookingService = new SessionBookingService(context, dtp, walletLockService, quotaService);
        var reconciliationService = new LedgerReconciliationService(context);

        // Apply bonus to initialize ledger for learner
        var walletService = new WalletService(context, dtp, walletLockService);
        await walletService.ApplyBonusAsync(learner.Id, 180, "Initial signup bonus");

        // Book 60m session
        var b = await bookingService.BookSessionAsync(learner.Id, new BookSessionRequest(
            swapReq.Id, dtp.UtcNow.AddDays(1), 60, SessionMode.Online, null, false));
        Assert.True(b.IsSuccess);

        var recResult = await reconciliationService.ReconcileWalletAsync(learner.Id);

        Assert.True(recResult.IsSuccess);
        Assert.True(recResult.Value.IsConsistent);
        Assert.Equal(recResult.Value.MaterializedAvailable, recResult.Value.CalculatedAvailable);
        Assert.Equal(recResult.Value.MaterializedHeld, recResult.Value.CalculatedHeld);
    }
}


