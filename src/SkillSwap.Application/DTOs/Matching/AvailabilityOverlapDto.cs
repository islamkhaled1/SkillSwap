namespace SkillSwap.Application.DTOs.Matching;

public sealed class AvailabilityOverlapDto
{
    public DayOfWeek DayOfWeek { get; init; }
    public TimeOnly OverlapStartTime { get; init; }
    public TimeOnly OverlapEndTime { get; init; }
}
