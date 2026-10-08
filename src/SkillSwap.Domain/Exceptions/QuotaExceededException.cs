namespace SkillSwap.Domain.Exceptions;

/// <summary>
/// Thrown when a learner attempts to book a session that exceeds their monthly learning quota.
/// </summary>
public class QuotaExceededException : DomainException
{
    public int RequestedMinutes { get; }
    public int UsedMinutes { get; }
    public int MonthlyLimitMinutes { get; }

    public QuotaExceededException(int requestedMinutes, int usedMinutes, int monthlyLimitMinutes)
        : base($"Monthly learning quota exceeded. Requested: {requestedMinutes}m, Already used: {usedMinutes}m, Monthly allowance: {monthlyLimitMinutes}m.")
    {
        RequestedMinutes = requestedMinutes;
        UsedMinutes = usedMinutes;
        MonthlyLimitMinutes = monthlyLimitMinutes;
    }
}
