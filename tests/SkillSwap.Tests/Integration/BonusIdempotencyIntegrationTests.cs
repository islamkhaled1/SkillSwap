using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Services;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Integration;

public class BonusIdempotencyIntegrationTests
{
    [Fact]
    public async Task ApplyBonusAsync_FirstRequest_GrantsMinutesAndStoresIdempotencyKey()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var user = await TestDbHelper.CreateUserAsync(context, "idemp_user_" + Guid.NewGuid().ToString("N")[..8]);
        var key = "test-key-" + Guid.NewGuid().ToString("N");

        var walletLockService = new SqlWalletLockService(context, dtp);
        var walletService = new WalletService(context, dtp, walletLockService);

        var result = await walletService.ApplyBonusAsync(user.Id, 60, "First grant", key);

        Assert.True(result.IsSuccess);
        Assert.Equal(60, result.Value.AvailableDelta);
        Assert.Equal(CreditTransactionType.Bonus, result.Value.Type);

        var wallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == user.Id);
        Assert.Equal(60, wallet.AvailableMinutes);

        var tx = await context.CreditTransactions.AsNoTracking().FirstAsync(t => t.IdempotencyKey == key);
        Assert.Equal(user.Id, tx.WalletId);
        Assert.Equal(60, tx.AvailableDelta);
        Assert.Equal("First grant", tx.Description);
    }

    [Fact]
    public async Task ApplyBonusAsync_ReplayWithIdenticalRequest_ReturnsExistingTransactionWithoutDoubleGrant()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var user = await TestDbHelper.CreateUserAsync(context, "idemp_replay_" + Guid.NewGuid().ToString("N")[..8]);
        var key = "replay-key-" + Guid.NewGuid().ToString("N");

        var walletLockService = new SqlWalletLockService(context, dtp);
        var walletService = new WalletService(context, dtp, walletLockService);

        var res1 = await walletService.ApplyBonusAsync(user.Id, 60, "Initial bonus", key);
        Assert.True(res1.IsSuccess);

        // Immediate replay with same parameters
        var res2 = await walletService.ApplyBonusAsync(user.Id, 60, "Initial bonus", key);
        Assert.True(res2.IsSuccess);

        // Both return the exact same transaction ID
        Assert.Equal(res1.Value.Id, res2.Value.Id);
        Assert.Equal(60, res2.Value.AvailableDelta);

        // Balance must remain strictly 60, NOT 120
        var wallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == user.Id);
        Assert.Equal(60, wallet.AvailableMinutes);

        // Exactly one ledger transaction exists for this key
        var txCount = await context.CreditTransactions.CountAsync(t => t.IdempotencyKey == key);
        Assert.Equal(1, txCount);
    }

    [Fact]
    public async Task ApplyBonusAsync_ReusedKeyWithDifferentAmount_ReturnsConflictWithoutMutatingWallet()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var user = await TestDbHelper.CreateUserAsync(context, "idemp_diff_amt_" + Guid.NewGuid().ToString("N")[..8]);
        var key = "diff-amt-key-" + Guid.NewGuid().ToString("N");

        var walletLockService = new SqlWalletLockService(context, dtp);
        var walletService = new WalletService(context, dtp, walletLockService);

        var res1 = await walletService.ApplyBonusAsync(user.Id, 60, "Legitimate grant", key);
        Assert.True(res1.IsSuccess);

        // Reusing key with different minutes
        var res2 = await walletService.ApplyBonusAsync(user.Id, 120, "Legitimate grant", key);
        Assert.True(res2.IsFailure);
        Assert.Contains("conflict", res2.Error, StringComparison.OrdinalIgnoreCase);

        // Balance remains 60
        var wallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == user.Id);
        Assert.Equal(60, wallet.AvailableMinutes);
    }

    [Fact]
    public async Task ApplyBonusAsync_ReusedKeyWithDifferentUser_ReturnsConflictWithoutMutatingTargetWallet()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var userA = await TestDbHelper.CreateUserAsync(context, "idemp_user_a_" + Guid.NewGuid().ToString("N")[..8]);
        var userB = await TestDbHelper.CreateUserAsync(context, "idemp_user_b_" + Guid.NewGuid().ToString("N")[..8]);
        var key = "cross-user-key-" + Guid.NewGuid().ToString("N");

        var walletLockService = new SqlWalletLockService(context, dtp);
        var walletService = new WalletService(context, dtp, walletLockService);

        var resA = await walletService.ApplyBonusAsync(userA.Id, 60, "User A grant", key);
        Assert.True(resA.IsSuccess);

        // Reusing key for User B
        var resB = await walletService.ApplyBonusAsync(userB.Id, 60, "User A grant", key);
        Assert.True(resB.IsFailure);
        Assert.Contains("conflict", resB.Error, StringComparison.OrdinalIgnoreCase);

        // User A balance = 60, User B balance = 0
        var walletA = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == userA.Id);
        Assert.Equal(60, walletA.AvailableMinutes);

        var walletB = await context.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == userB.Id);
        Assert.True(walletB == null || walletB.AvailableMinutes == 0);
    }

    [Fact]
    public async Task ApplyBonusAsync_DifferentKeys_AllowDistinctLegitimateGrants()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var user = await TestDbHelper.CreateUserAsync(context, "idemp_multi_" + Guid.NewGuid().ToString("N")[..8]);

        var key1 = "multi-grant-1-" + Guid.NewGuid().ToString("N");
        var key2 = "multi-grant-2-" + Guid.NewGuid().ToString("N");

        var walletLockService = new SqlWalletLockService(context, dtp);
        var walletService = new WalletService(context, dtp, walletLockService);

        var res1 = await walletService.ApplyBonusAsync(user.Id, 60, "Grant 1", key1);
        Assert.True(res1.IsSuccess);

        var res2 = await walletService.ApplyBonusAsync(user.Id, 30, "Grant 2", key2);
        Assert.True(res2.IsSuccess);

        var wallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == user.Id);
        Assert.Equal(90, wallet.AvailableMinutes);

        var txs = await context.CreditTransactions.AsNoTracking().Where(t => t.WalletId == user.Id).ToListAsync();
        Assert.Equal(2, txs.Count);
    }

    [Fact]
    public async Task SqlServerUniqueIndex_EnforcesUniqueness_ThrowsOnDuplicateKeyInsert()
    {
        using var context = TestDbContextFactory.CreateSqlServerContext();
        var dtp = new TestDateTimeProvider();
        var user = await TestDbHelper.CreateUserAsync(context, "idemp_raw_idx_" + Guid.NewGuid().ToString("N")[..8]);
        var key = "raw-idx-key-" + Guid.NewGuid().ToString("N");

        context.Wallets.Add(new Wallet
        {
            UserId = user.Id,
            AvailableMinutes = 0,
            HeldMinutes = 0,
            UpdatedAtUtc = dtp.UtcNow
        });
        await context.SaveChangesAsync();

        context.CreditTransactions.Add(new CreditTransaction
        {
            WalletId = user.Id,
            Type = CreditTransactionType.Bonus,
            AvailableDelta = 60,
            HeldDelta = 0,
            CreatedAtUtc = dtp.UtcNow,
            Description = "Direct insert 1",
            IdempotencyKey = key
        });
        await context.SaveChangesAsync();

        // Attempting to insert a duplicate with the same non-null IdempotencyKey must violate the unique index
        context.CreditTransactions.Add(new CreditTransaction
        {
            WalletId = user.Id,
            Type = CreditTransactionType.Bonus,
            AvailableDelta = 60,
            HeldDelta = 0,
            CreatedAtUtc = dtp.UtcNow,
            Description = "Direct insert 2",
            IdempotencyKey = key
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.True(ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx &&
                    (sqlEx.Number == 2601 || sqlEx.Number == 2627));
        Assert.Contains("UX_CreditTransaction_IdempotencyKey", ex.InnerException.Message);
    }

    [Fact]
    public async Task ConcurrentDuplicateRequests_SameKeyAndPayload_NeverDoubleGrantMinutes()
    {
        var dtp = new TestDateTimeProvider();
        using var setupContext = TestDbContextFactory.CreateSqlServerContext();
        var user = await TestDbHelper.CreateUserAsync(setupContext, "idemp_concurr_" + Guid.NewGuid().ToString("N")[..8]);
        var key = "concurrent-key-" + Guid.NewGuid().ToString("N");

        const int concurrencyDegree = 6;
        var tasks = Enumerable.Range(0, concurrencyDegree).Select(async _ =>
        {
            using var taskContext = TestDbContextFactory.CreateSqlServerContext();
            var taskLockService = new SqlWalletLockService(taskContext, dtp);
            var taskWalletService = new WalletService(taskContext, dtp, taskLockService);

            return await taskWalletService.ApplyBonusAsync(user.Id, 60, "Concurrent grant", key);
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Every concurrent invocation must complete successfully (either as the initial winner or idempotent replay)
        foreach (var r in results)
        {
            Assert.True(r.IsSuccess);
            Assert.Equal(60, r.Value.AvailableDelta);
        }

        // Final wallet available minutes must be strictly 60 (never 360)
        using var verifyContext = TestDbContextFactory.CreateSqlServerContext();
        var wallet = await verifyContext.Wallets.AsNoTracking().FirstAsync(w => w.UserId == user.Id);
        Assert.Equal(60, wallet.AvailableMinutes);

        // Exactly one ledger row must exist for this key
        var txCount = await verifyContext.CreditTransactions.CountAsync(t => t.IdempotencyKey == key);
        Assert.Equal(1, txCount);
    }
}
