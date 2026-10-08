using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Wallets;

namespace SkillSwap.Application.Abstractions;

public interface ILedgerReconciliationService
{
    Task<Result<WalletReconciliationResult>> ReconcileWalletAsync(Guid userId, CancellationToken cancellationToken = default);
}
