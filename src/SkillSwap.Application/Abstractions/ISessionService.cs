using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Sessions;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Abstractions;

public interface ISessionService
{
    Task<Result<SessionDto>> GetSessionByIdAsync(long sessionId, Guid requestingUserId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SessionDto>>> GetUserSessionsAsync(Guid userId, SessionStatus? statusFilter = null, CancellationToken cancellationToken = default);
    Task<Result<SessionDto>> ConfirmSessionAsync(long sessionId, Guid teacherId, CancellationToken cancellationToken = default);
    Task<Result<SessionDto>> CancelSessionAsync(long sessionId, Guid requestingUserId, string? reason = null, CancellationToken cancellationToken = default);
    Task<Result<SessionDto>> MarkJoinedAsync(long sessionId, Guid requestingUserId, CancellationToken cancellationToken = default);
    Task<Result<SessionDto>> MarkNoShowAsync(long sessionId, Guid requestingUserId, NoShowParty noShowParty, CancellationToken cancellationToken = default);
    Task<Result<SessionDto>> MarkNoShowAsync(long sessionId, Guid requestingUserId, NoShowParty noShowParty, bool isAdmin, CancellationToken cancellationToken = default);
    Task<Result<SessionDto>> ReportDisputeAsync(long sessionId, Guid requestingUserId, string reason, CancellationToken cancellationToken = default);
    Task<Result<SessionDto>> ResolveDisputeAsync(long sessionId, Guid adminId, bool awardTeacher, string resolutionNote, CancellationToken cancellationToken = default);
}
