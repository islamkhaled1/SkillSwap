using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Wallets;

namespace SkillSwap.Application.Services;

public class LedgerReconciliationService : ILedgerReconciliationService
{
    private readonly IApplicationDbContext _context;

    public LedgerReconciliationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<WalletReconciliationResult>> ReconcileWalletAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var wallet = await _context.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);

        if (wallet == null)
            return Result.Failure<WalletReconciliationResult>($"Wallet for user {userId} was not found.");

        var ledgerEntries = await _context.CreditTransactions
            .AsNoTracking()
            .Where(ct => ct.WalletId == userId)
            .ToListAsync(cancellationToken);

        var calculatedAvailable = ledgerEntries.Sum(ct => ct.AvailableDelta);
        var calculatedHeld = ledgerEntries.Sum(ct => ct.HeldDelta);

        var isConsistent = (wallet.AvailableMinutes == calculatedAvailable) && (wallet.HeldMinutes == calculatedHeld);
        var details = isConsistent
            ? "Wallet balance perfectly matches the sum of all append-only ledger entries."
            : $"Mismatch detected! Available: Materialized={wallet.AvailableMinutes}, Ledger={calculatedAvailable}. Held: Materialized={wallet.HeldMinutes}, Ledger={calculatedHeld}.";

        var result = new WalletReconciliationResult(
            userId,
            wallet.AvailableMinutes,
            wallet.HeldMinutes,
            calculatedAvailable,
            calculatedHeld,
            isConsistent,
            details);

        return Result.Success(result);
    }
}
