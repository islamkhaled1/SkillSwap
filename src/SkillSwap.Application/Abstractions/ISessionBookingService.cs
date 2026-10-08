using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Sessions;

namespace SkillSwap.Application.Abstractions;

public interface ISessionBookingService
{
    Task<Result<SessionDto>> BookSessionAsync(Guid learnerId, BookSessionRequest request, CancellationToken cancellationToken = default);
}
