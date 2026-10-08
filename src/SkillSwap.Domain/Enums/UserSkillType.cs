namespace SkillSwap.Domain.Enums;

/// <summary>
/// Whether the user offers to teach or wants to learn a skill.
/// </summary>
public enum UserSkillType : byte
{
    Teach = 1,
    Learn = 2,
    Teaching = 1, // Backward compatibility alias
    Learning = 2  // Backward compatibility alias
}
