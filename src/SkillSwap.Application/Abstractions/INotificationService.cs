using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Notifications;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Abstractions;

public interface INotificationService
{
    Task<Result<IReadOnlyList<NotificationDto>>> GetUserNotificationsAsync(
        Guid userId,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default);

    Task<Result> MarkAsReadAsync(
        Guid userId,
        long notificationId,
        CancellationToken cancellationToken = default);

    Task<Result<NotificationDto>> CreateNotificationAsync(
        Guid userId,
        NotificationKind type,
        string payload,
        CancellationToken cancellationToken = default);
}
