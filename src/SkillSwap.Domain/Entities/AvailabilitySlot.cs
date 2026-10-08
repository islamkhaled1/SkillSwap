namespace SkillSwap.Domain.Entities;

public class AvailabilitySlot
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
