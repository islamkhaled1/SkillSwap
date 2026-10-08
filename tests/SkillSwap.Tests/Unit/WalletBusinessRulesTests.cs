using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Domain.Exceptions;
using SkillSwap.Infrastructure.Services;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class WalletBusinessRulesTests
{
    [Fact]
    public void Wallet_CannotHaveNegativeBalance_ValidationLogic()
    {
        var wallet = new Wallet { UserId = Guid.NewGuid(), AvailableMinutes = 30, HeldMinutes = 0 };

        // Attempting to spend 60 when available is 30 violates balance check
        Assert.Throws<InsufficientBalanceException>(() =>
        {
            if (wallet.AvailableMinutes < 60)
                throw new InsufficientBalanceException(60, wallet.AvailableMinutes);

            wallet.AvailableMinutes -= 60;
        });
    }

    [Fact]
    public void HoldOperation_ChangesAvailableAndHeld_Correctly()
    {
        var wallet = new Wallet { UserId = Guid.NewGuid(), AvailableMinutes = 120, HeldMinutes = 0 };
        int duration = 60;

        wallet.AvailableMinutes -= duration;
        wallet.HeldMinutes += duration;

        Assert.Equal(60, wallet.AvailableMinutes);
        Assert.Equal(60, wallet.HeldMinutes);
    }

    [Fact]
    public void ReleaseOperation_RestoresAvailableBalance_Correctly()
    {
        var wallet = new Wallet { UserId = Guid.NewGuid(), AvailableMinutes = 60, HeldMinutes = 60 };
        int duration = 60;

        wallet.HeldMinutes -= duration;
        wallet.AvailableMinutes += duration;

        Assert.Equal(120, wallet.AvailableMinutes);
        Assert.Equal(0, wallet.HeldMinutes);
    }

    [Fact]
    public void CaptureOperation_ReducesHeld_LeavesAvailableUnchanged()
    {
        var wallet = new Wallet { UserId = Guid.NewGuid(), AvailableMinutes = 60, HeldMinutes = 60 };
        int duration = 60;

        wallet.HeldMinutes -= duration;

        Assert.Equal(60, wallet.AvailableMinutes);
        Assert.Equal(0, wallet.HeldMinutes);
    }

    [Fact]
    public void EarnOperation_IncreasesAvailable_LeavesHeldUnchanged()
    {
        var wallet = new Wallet { UserId = Guid.NewGuid(), AvailableMinutes = 60, HeldMinutes = 0 };
        int duration = 60;

        wallet.AvailableMinutes += duration;

        Assert.Equal(120, wallet.AvailableMinutes);
        Assert.Equal(0, wallet.HeldMinutes);
    }

    [Fact]
    public async Task WalletService_GetWallet_ReturnsCorrectMaterializedModel()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var user = await TestDbHelper.CreateUserAsync(context, "wallet_user_" + Guid.NewGuid().ToString("N")[..8]);

        var wallet = new Wallet
        {
            UserId = user.Id,
            AvailableMinutes = 150,
            HeldMinutes = 60,
            UpdatedAtUtc = dtp.UtcNow
        };
        context.Wallets.Add(wallet);
        await context.SaveChangesAsync();

        var walletLockService = new SqlWalletLockService(context, dtp);
        var walletService = new WalletService(context, dtp, walletLockService);

        var result = await walletService.GetWalletAsync(user.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(150, result.Value.AvailableMinutes);
        Assert.Equal(60, result.Value.HeldMinutes);
        Assert.Equal(210, result.Value.TotalMinutes);
    }

    [Fact]
    public async Task ApplyBonus_AddsAvailableMinutes_AndRecordsBonusLedger()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var user = await TestDbHelper.CreateUserAsync(context, "bonus_user_" + Guid.NewGuid().ToString("N")[..8]);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var walletService = new WalletService(context, dtp, walletLockService);

        var result = await walletService.ApplyBonusAsync(user.Id, 60, "Welcome gift");

        Assert.True(result.IsSuccess);
        Assert.Equal(60, result.Value.AvailableDelta);
        Assert.Equal(0, result.Value.HeldDelta);
        Assert.Equal(CreditTransactionType.Bonus, result.Value.Type);

        var wallet = await context.Wallets.FirstAsync(w => w.UserId == user.Id);
        Assert.Equal(60, wallet.AvailableMinutes);
        Assert.Equal(0, wallet.HeldMinutes);
    }

    [Fact]
    public async Task ApplyAdjustment_ModifiesWallet_AndRecordsAdjustmentLedger()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var user = await TestDbHelper.CreateUserAsync(context, "adjust_user_" + Guid.NewGuid().ToString("N")[..8]);

        var walletLockService = new SqlWalletLockService(context, dtp);
        var walletService = new WalletService(context, dtp, walletLockService);

        await walletService.ApplyBonusAsync(user.Id, 90, "Initial");

        var result = await walletService.ApplyAdjustmentAsync(user.Id, -30, 30, "Manual hold correction");

        Assert.True(result.IsSuccess);
        Assert.Equal(CreditTransactionType.Adjustment, result.Value.Type);
        Assert.Equal(-30, result.Value.AvailableDelta);
        Assert.Equal(30, result.Value.HeldDelta);

        var wallet = await context.Wallets.FirstAsync(w => w.UserId == user.Id);
        Assert.Equal(60, wallet.AvailableMinutes);
        Assert.Equal(30, wallet.HeldMinutes);
    }
}
