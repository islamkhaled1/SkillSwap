namespace SkillSwap.Domain.Enums;

/// <summary>
/// Types of notifications sent to users.
/// </summary>
public enum NotificationKind : byte
{
    SwapRequest = 1,
    SessionReminder = 2,
    SessionCompleted = 3,
    SessionCancelled = 4,
    NewMessage = 5,
    ReviewReceived = 6,
    BadgeEarned = 7,
    System = 8
}
