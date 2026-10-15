namespace SkillSwap.Application.DTOs.Availability;

public sealed class UpdateAvailabilityRequest
{
    public IReadOnlyList<AvailabilitySlotRequest> Slots { get; init; } = [];
}
