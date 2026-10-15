using System.ComponentModel.DataAnnotations;

namespace SkillSwap.Application.DTOs.Availability;

public sealed class AvailabilitySlotRequest
{
    [EnumDataType(typeof(DayOfWeek), ErrorMessage = "A valid DayOfWeek (0=Sunday ... 6=Saturday) is required.")]
    public DayOfWeek DayOfWeek { get; init; }

    [Required(ErrorMessage = "StartTime is required.")]
    public TimeOnly StartTime { get; init; }

    [Required(ErrorMessage = "EndTime is required.")]
    public TimeOnly EndTime { get; init; }
}
