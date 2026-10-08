using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Wallets;

/// <summary>
/// Represents an entry in the append-only credit ledger.
/// </summary>
public record CreditTransactionDto(
    long Id,
    Guid WalletId,
    long? SessionId,
    CreditTransactionType Type,
    int AvailableDelta,
    int HeldDelta,
    DateTime CreatedAtUtc,
    string? Description);
