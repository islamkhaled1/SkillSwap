namespace SkillSwap.Domain.Enums;

/// <summary>
/// Reason for submitting a user report.
/// </summary>
public enum ReportReason : byte
{
    Harassment = 1,
    InappropriateContent = 2,
    Spam = 3,
    NoShow = 4,
    Fraud = 5,
    Other = 6
}
