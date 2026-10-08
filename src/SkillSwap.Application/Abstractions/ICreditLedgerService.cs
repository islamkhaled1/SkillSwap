using SkillSwap.Application.DTOs.Wallets;

namespace SkillSwap.Application.Abstractions;

public interface ICreditLedgerService
{
    Task<IReadOnlyList<CreditTransactionDto>> GetTransactionsByWalletAsync(Guid walletId, int count = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CreditTransactionDto>> GetTransactionsBySessionAsync(long sessionId, CancellationToken cancellationToken = default);
}
