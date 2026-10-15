namespace SkillSwap.Application.DTOs.Availability;

public sealed class AvailabilitySlotDto
{
    public long Id { get; init; }
    public Guid UserId { get; init; }
    public DayOfWeek DayOfWeek { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
