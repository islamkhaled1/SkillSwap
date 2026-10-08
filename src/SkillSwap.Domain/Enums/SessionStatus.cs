namespace SkillSwap.Domain.Enums;

/// <summary>
/// Status of a session. InProgress is derived from time, not stored.
/// </summary>
public enum SessionStatus : byte
{
    PendingConfirmation = 1,
    Scheduled = 2,
    Completed = 3,
    Cancelled = 4,
    NoShow = 5,
    Disputed = 6,
    Expired = 7
}
