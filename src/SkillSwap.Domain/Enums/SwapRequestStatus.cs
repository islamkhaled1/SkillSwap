namespace SkillSwap.Domain.Enums;

/// <summary>
/// Lifecycle states of a SwapRequest.
/// Note: SwapRequest does NOT have a Completed status.
/// </summary>
public enum SwapRequestStatus : byte
{
    Pending = 1,
    Accepted = 2,
    Declined = 3,
    Cancelled = 4,
    Expired = 5
}
