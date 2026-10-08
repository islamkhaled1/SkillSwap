namespace SkillSwap.Application.Common.Options;

/// <summary>
/// Centralized configuration options for session lifecycle policies.
/// </summary>
public class SessionPolicyOptions
{
    public const string SectionName = "SessionPolicies";

    /// <summary>
    /// Duration in hours before an unconfirmed session in PendingConfirmation expires.
    /// Business rule default: ~24 hours.
    /// </summary>
    public int PendingConfirmationHours { get; set; } = 24;

    /// <summary>
    /// Grace period in minutes after Session.EndUtc before the auto-completion background job
    /// transitions the session to Completed and transfers credits.
    /// Default: 15 minutes.
    /// </summary>
    public int AutoCompletionGracePeriodMinutes { get; set; } = 15;

    /// <summary>
    /// Days before an unanswered pending SwapRequest expires.
    /// Business rule default: ~7 days.
    /// </summary>
    public int OldSwapRequestExpirationDays { get; set; } = 7;
}
