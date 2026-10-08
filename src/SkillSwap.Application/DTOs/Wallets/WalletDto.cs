namespace SkillSwap.Application.DTOs.Wallets;

/// <summary>
/// Represents the current materialized balance of a user wallet.
/// </summary>
public record WalletDto(
    Guid UserId,
    int AvailableMinutes,
    int HeldMinutes,
    int TotalMinutes,
    DateTime UpdatedAtUtc);
