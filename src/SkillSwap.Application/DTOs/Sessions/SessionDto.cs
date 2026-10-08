using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Sessions;

/// <summary>
/// Full representation of a session.
/// </summary>
public record SessionDto(
    long Id,
    long SwapRequestId,
    Guid TeacherId,
    Guid LearnerId,
    int SkillId,
    DateTime StartUtc,
    DateTime EndUtc,
    short DurationMinutes,
    SessionMode Mode,
    SessionStatus Status,
    string? MeetingUrl,
    Guid? CancelledById,
    DateTime? CancelledAtUtc,
    Guid? NoShowUserId,
    DateTime? LearnerJoinedAtUtc,
    DateTime? TeacherJoinedAtUtc,
    Guid? ReportedById,
    DateTime? ResolvedAtUtc,
    string? ResolutionNote,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
