namespace SkillSwap.Domain.Enums;

/// <summary>
/// How the session is conducted. MVP uses Online only.
/// </summary>
public enum SessionMode : byte
{
    Online = 1,
    InPerson = 2
}
