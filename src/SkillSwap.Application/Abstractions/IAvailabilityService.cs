using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Availability;

namespace SkillSwap.Application.Abstractions;

public interface IAvailabilityService
{
    Task<Result<IReadOnlyList<AvailabilitySlotDto>>> GetUserAvailabilityAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AvailabilitySlotDto>>> UpdateUserAvailabilityAsync(Guid userId, UpdateAvailabilityRequest request, CancellationToken cancellationToken = default);
}
