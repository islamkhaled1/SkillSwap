using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Domain.Entities;

namespace SkillSwap.Infrastructure.Services;

public class SqlWalletLockService : IWalletLockService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SqlWalletLockService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<(Wallet First, Wallet Second)> AcquireWalletLocksAsync(
        Guid userId1,
        Guid userId2,
        CancellationToken cancellationToken = default)
    {
        // Sort deterministically to guarantee consistent lock hierarchy across all threads
        var (firstId, secondId) = userId1.CompareTo(userId2) < 0
            ? (userId1, userId2)
            : (userId2, userId1);

        var first = await LockSingleWalletAsync(firstId, cancellationToken);
        var second = firstId == secondId
            ? first
            : await LockSingleWalletAsync(secondId, cancellationToken);

        var wallet1 = first.UserId == userId1 ? first : second;
        var wallet2 = second.UserId == userId2 ? second : first;

        return (wallet1, wallet2);
    }

    public Task<Wallet> AcquireWalletLockAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return LockSingleWalletAsync(userId, cancellationToken);
    }

    private async Task<Wallet> LockSingleWalletAsync(Guid userId, CancellationToken cancellationToken)
    {
        Wallet? wallet;

        if (_context.Database.IsSqlServer())
        {
            // Acquire parameterized SQL Server row update lock
            wallet = await _context.Wallets
                .FromSqlInterpolated($"SELECT * FROM Wallets WITH (UPDLOCK, ROWLOCK) WHERE UserId = {userId}")
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);
        }

        if (wallet == null)
        {
            wallet = new Wallet
            {
                UserId = userId,
                AvailableMinutes = 0,
                HeldMinutes = 0,
                UpdatedAtUtc = _dateTimeProvider.UtcNow
            };

            _context.Wallets.Add(wallet);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                if (_context.Database.IsSqlServer())
                {
                    wallet = await _context.Wallets
                        .FromSqlInterpolated($"SELECT * FROM Wallets WITH (UPDLOCK, ROWLOCK) WHERE UserId = {userId}")
                        .FirstAsync(cancellationToken);
                }
                else
                {
                    wallet = await _context.Wallets
                        .FirstAsync(w => w.UserId == userId, cancellationToken);
                }
            }
        }

        return wallet;
    }
}
