using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Wallets;
using SkillSwap.Domain.Entities;

namespace SkillSwap.Application.Abstractions;

public interface IWalletService
{
    Task<Result<WalletDto>> GetWalletAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<CreditTransactionDto>>> GetTransactionsAsync(Guid userId, int count = 50, CancellationToken cancellationToken = default);
    Task<Wallet> GetOrCreateWalletAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<CreditTransactionDto>> ApplyBonusAsync(Guid userId, int minutes, string description, CancellationToken cancellationToken = default);
    Task<Result<CreditTransactionDto>> ApplyAdjustmentAsync(Guid userId, int availableDelta, int heldDelta, string description, CancellationToken cancellationToken = default);
}
