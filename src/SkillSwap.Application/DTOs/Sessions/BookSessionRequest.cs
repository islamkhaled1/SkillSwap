using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Sessions;

/// <summary>
/// Payload to book a new learning session.
/// Requester is the learner; Receiver is the teacher.
/// </summary>
public record BookSessionRequest(
    long SwapRequestId,
    DateTime StartUtc,
    short DurationMinutes,
    SessionMode Mode = SessionMode.Online,
    string? MeetingUrl = null,
    bool RequireTeacherConfirmation = false,
    int? SkillId = null);
