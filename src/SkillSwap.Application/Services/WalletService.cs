using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Wallets;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Domain.Exceptions;

namespace SkillSwap.Application.Services;

public class WalletService : IWalletService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IWalletLockService _walletLockService;

    public WalletService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider,
        IWalletLockService walletLockService)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
        _walletLockService = walletLockService;
    }

    public async Task<Wallet> GetOrCreateWalletAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);
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
                // Concurrency catch: if another request created it in parallel, read it
                wallet = await _context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == userId, cancellationToken);
            }
        }
        return wallet;
    }

    public async Task<Result<WalletDto>> GetWalletAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var wallet = await GetOrCreateWalletAsync(userId, cancellationToken);
        var total = wallet.AvailableMinutes + wallet.HeldMinutes;

        var dto = new WalletDto(
            wallet.UserId,
            wallet.AvailableMinutes,
            wallet.HeldMinutes,
            total,
            wallet.UpdatedAtUtc);

        return Result.Success(dto);
    }

    public async Task<Result<IReadOnlyList<CreditTransactionDto>>> GetTransactionsAsync(Guid userId, int count = 50, CancellationToken cancellationToken = default)
    {
        var txs = await _context.CreditTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == userId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(count)
            .Select(t => new CreditTransactionDto(
                t.Id,
                t.WalletId,
                t.SessionId,
                t.Type,
                t.AvailableDelta,
                t.HeldDelta,
                t.CreatedAtUtc,
                t.Description))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CreditTransactionDto>>(txs);
    }

    public async Task<Result<CreditTransactionDto>> ApplyBonusAsync(Guid userId, int minutes, string description, CancellationToken cancellationToken = default)
    {
        if (minutes <= 0)
            return Result.Failure<CreditTransactionDto>("Bonus minutes must be greater than zero.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var wallet = await _walletLockService.AcquireWalletLockAsync(userId, cancellationToken);
        wallet.AvailableMinutes += minutes;
        wallet.UpdatedAtUtc = _dateTimeProvider.UtcNow;

        var ledgerEntry = new CreditTransaction
        {
            WalletId = userId,
            SessionId = null,
            Type = CreditTransactionType.Bonus,
            AvailableDelta = minutes,
            HeldDelta = 0,
            CreatedAtUtc = _dateTimeProvider.UtcNow,
            Description = description
        };

        _context.CreditTransactions.Add(ledgerEntry);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var dto = new CreditTransactionDto(
            ledgerEntry.Id,
            ledgerEntry.WalletId,
            ledgerEntry.SessionId,
            ledgerEntry.Type,
            ledgerEntry.AvailableDelta,
            ledgerEntry.HeldDelta,
            ledgerEntry.CreatedAtUtc,
            ledgerEntry.Description);

        return Result.Success(dto);
    }

    public async Task<Result<CreditTransactionDto>> ApplyAdjustmentAsync(Guid userId, int availableDelta, int heldDelta, string description, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var wallet = await _walletLockService.AcquireWalletLockAsync(userId, cancellationToken);

        if (wallet.AvailableMinutes + availableDelta < 0)
            throw new InsufficientBalanceException(Math.Abs(availableDelta), wallet.AvailableMinutes);

        if (wallet.HeldMinutes + heldDelta < 0)
            return Result.Failure<CreditTransactionDto>("Adjustment would result in negative HeldMinutes.");

        wallet.AvailableMinutes += availableDelta;
        wallet.HeldMinutes += heldDelta;
        wallet.UpdatedAtUtc = _dateTimeProvider.UtcNow;

        var ledgerEntry = new CreditTransaction
        {
            WalletId = userId,
            SessionId = null,
            Type = CreditTransactionType.Adjustment,
            AvailableDelta = availableDelta,
            HeldDelta = heldDelta,
            CreatedAtUtc = _dateTimeProvider.UtcNow,
            Description = description
        };

        _context.CreditTransactions.Add(ledgerEntry);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var dto = new CreditTransactionDto(
            ledgerEntry.Id,
            ledgerEntry.WalletId,
            ledgerEntry.SessionId,
            ledgerEntry.Type,
            ledgerEntry.AvailableDelta,
            ledgerEntry.HeldDelta,
            ledgerEntry.CreatedAtUtc,
            ledgerEntry.Description);

        return Result.Success(dto);
    }
}
