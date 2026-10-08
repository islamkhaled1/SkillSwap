namespace SkillSwap.Domain.Enums;

/// <summary>
/// Indicates which party or parties failed to attend the scheduled session.
/// </summary>
public enum NoShowParty : byte
{
    Learner = 1,
    Teacher = 2,
    Both = 3
}
