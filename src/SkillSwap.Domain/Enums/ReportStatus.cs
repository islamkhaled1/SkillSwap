namespace SkillSwap.Domain.Enums;

/// <summary>
/// Status of a user report.
/// </summary>
public enum ReportStatus : byte
{
    Open = 1,
    UnderReview = 2,
    Resolved = 3,
    Rejected = 4
}
