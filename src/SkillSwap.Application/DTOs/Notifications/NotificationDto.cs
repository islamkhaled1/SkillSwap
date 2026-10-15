using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Notifications;

public sealed class NotificationDto
{
    public long Id { get; init; }
    public Guid UserId { get; init; }
    public NotificationKind Type { get; init; }
    public string Payload { get; init; } = string.Empty;
    public bool IsRead { get; init; }
    public DateTime? ReadAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
