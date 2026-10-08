namespace SkillSwap.Application.Abstractions;

/// <summary>
/// Abstraction over DateTime.UtcNow for testability.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
