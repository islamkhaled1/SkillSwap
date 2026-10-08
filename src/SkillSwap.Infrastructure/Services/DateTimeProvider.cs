using SkillSwap.Application.Abstractions;

namespace SkillSwap.Infrastructure.Services;

/// <summary>
/// Production implementation of IDateTimeProvider using UTC system time.
/// </summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
