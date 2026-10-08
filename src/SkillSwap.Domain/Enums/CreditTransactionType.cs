namespace SkillSwap.Domain.Enums;

/// <summary>
/// Types in the append-only credit ledger.
/// </summary>
public enum CreditTransactionType : byte
{
    Bonus = 1,
    Hold = 2,
    Release = 3,
    Capture = 4,
    Earn = 5,
    Adjustment = 6
}
