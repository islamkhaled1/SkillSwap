using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Sessions;

namespace SkillSwap.Application.Abstractions;

public interface ISessionCompletionService
{
    Task<Result<SessionDto>> CompleteSessionAsync(long sessionId, Guid requestingUserId, CancellationToken cancellationToken = default);
    Task<Result<SessionDto>> AutoCompleteSessionAsync(long sessionId, CancellationToken cancellationToken = default);
    Task<Result<SessionDto>> ExpirePendingSessionAsync(long sessionId, CancellationToken cancellationToken = default);
    Task<int> AutoCompleteEligibleSessionsAsync(CancellationToken cancellationToken = default);
    Task<int> ExpirePendingSessionsAsync(CancellationToken cancellationToken = default);
}
