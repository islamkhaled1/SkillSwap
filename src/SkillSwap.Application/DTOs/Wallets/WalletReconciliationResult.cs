namespace SkillSwap.Application.DTOs.Wallets;

/// <summary>
/// Represents the audit comparison between materialized wallet balance and calculated ledger deltas.
/// </summary>
public record WalletReconciliationResult(
    Guid UserId,
    int MaterializedAvailable,
    int MaterializedHeld,
    int CalculatedAvailable,
    int CalculatedHeld,
    bool IsConsistent,
    string Details);
