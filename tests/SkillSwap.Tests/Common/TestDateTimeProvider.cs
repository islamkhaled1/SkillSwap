using SkillSwap.Application.Abstractions;

namespace SkillSwap.Tests.Common;

public class TestDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
}
