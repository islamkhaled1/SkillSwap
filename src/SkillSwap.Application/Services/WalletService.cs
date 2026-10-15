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

    public async Task<Result<CreditTransactionDto>> ApplyBonusAsync(
        Guid userId,
        int minutes,
        string description,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        if (minutes <= 0)
            return Result.Failure<CreditTransactionDto>("Bonus minutes must be greater than zero.");

        // Fast-path pre-check for existing idempotency key before acquiring transaction locks
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _context.CreditTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(ct => ct.IdempotencyKey == idempotencyKey, cancellationToken);

            if (existing != null)
            {
                return ValidateAndMapExistingBonus(existing, userId, minutes, description);
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var wallet = await _walletLockService.AcquireWalletLockAsync(userId, cancellationToken);

            // Double-check under lock in case key was committed by an immediately preceding caller
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var existingUnderLock = await _context.CreditTransactions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ct => ct.IdempotencyKey == idempotencyKey, cancellationToken);

                if (existingUnderLock != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ValidateAndMapExistingBonus(existingUnderLock, userId, minutes, description);
                }
            }

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
                Description = description,
                IdempotencyKey = idempotencyKey
            };

            _context.CreditTransactions.Add(ledgerEntry);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Success(MapToDto(ledgerEntry));
        }
        catch (DbUpdateException ex) when (!string.IsNullOrWhiteSpace(idempotencyKey) && IsIdempotencyKeyUniqueViolation(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            (_context as DbContext)?.ChangeTracker.Clear();

            // Re-read winning ledger record safely after rollback
            var winning = await _context.CreditTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(ct => ct.IdempotencyKey == idempotencyKey, cancellationToken);

            if (winning != null)
            {
                return ValidateAndMapExistingBonus(winning, userId, minutes, description);
            }

            throw;
        }
    }

    private static Result<CreditTransactionDto> ValidateAndMapExistingBonus(
        CreditTransaction existing,
        Guid userId,
        int minutes,
        string description)
    {
        if (existing.WalletId == userId &&
            existing.AvailableDelta == minutes &&
            existing.Description == description &&
            existing.Type == CreditTransactionType.Bonus)
        {
            return Result.Success(MapToDto(existing));
        }

        return Result.Failure<CreditTransactionDto>(
            $"Idempotency key conflict: The key '{existing.IdempotencyKey}' has already been used for a different bonus grant.");
    }

    private static CreditTransactionDto MapToDto(CreditTransaction t) =>
        new(t.Id, t.WalletId, t.SessionId, t.Type, t.AvailableDelta, t.HeldDelta, t.CreatedAtUtc, t.Description);

    private static bool IsIdempotencyKeyUniqueViolation(DbUpdateException ex)
    {
        Exception? current = ex;
        while (current != null)
        {
            var message = current.Message;
            if (current.GetType().Name == "SqlException")
            {
                if (message.Contains("2601") || message.Contains("2627") ||
                    message.Contains("UX_CreditTransaction_IdempotencyKey", StringComparison.OrdinalIgnoreCase))
                {
                    return message.Contains("UX_CreditTransaction_IdempotencyKey", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("IdempotencyKey", StringComparison.OrdinalIgnoreCase);
                }
            }

            if (message.Contains("UX_CreditTransaction_IdempotencyKey", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
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
