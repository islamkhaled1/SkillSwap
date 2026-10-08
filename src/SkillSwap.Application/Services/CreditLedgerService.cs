using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Wallets;

namespace SkillSwap.Application.Services;

public class CreditLedgerService : ICreditLedgerService
{
    private readonly IApplicationDbContext _context;

    public CreditLedgerService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CreditTransactionDto>> GetTransactionsByWalletAsync(Guid walletId, int count = 50, CancellationToken cancellationToken = default)
    {
        var transactions = await _context.CreditTransactions
            .AsNoTracking()
            .Where(ct => ct.WalletId == walletId)
            .OrderByDescending(ct => ct.CreatedAtUtc)
            .Take(count)
            .Select(ct => new CreditTransactionDto(
                ct.Id,
                ct.WalletId,
                ct.SessionId,
                ct.Type,
                ct.AvailableDelta,
                ct.HeldDelta,
                ct.CreatedAtUtc,
                ct.Description))
            .ToListAsync(cancellationToken);

        return transactions;
    }

    public async Task<IReadOnlyList<CreditTransactionDto>> GetTransactionsBySessionAsync(long sessionId, CancellationToken cancellationToken = default)
    {
        var transactions = await _context.CreditTransactions
            .AsNoTracking()
            .Where(ct => ct.SessionId == sessionId)
            .OrderBy(ct => ct.CreatedAtUtc)
            .Select(ct => new CreditTransactionDto(
                ct.Id,
                ct.WalletId,
                ct.SessionId,
                ct.Type,
                ct.AvailableDelta,
                ct.HeldDelta,
                ct.CreatedAtUtc,
                ct.Description))
            .ToListAsync(cancellationToken);

        return transactions;
    }
}
