using SkillSwap.Domain.Entities;

namespace SkillSwap.Application.Abstractions;

/// <summary>
/// Infrastructure contract for acquiring deterministic row-level update locks (UPDLOCK) on wallets.
/// </summary>
public interface IWalletLockService
{
    /// <summary>
    /// Acquires row-level update locks on both wallet rows in deterministic UserId order
    /// to prevent deadlocks, race conditions, and concurrent over-spending.
    /// </summary>
    Task<(Wallet First, Wallet Second)> AcquireWalletLocksAsync(
        Guid userId1,
        Guid userId2,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a row-level update lock on a single wallet row.
    /// </summary>
    Task<Wallet> AcquireWalletLockAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
