using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Notifications;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Tests.Unit;

public class NotificationsControllerTests
{
    private class FakeNotificationService : INotificationService
    {
        public Func<Guid, bool, Task<Result<IReadOnlyList<NotificationDto>>>>? OnGetUserNotifications { get; set; }
        public Func<Guid, long, Task<Result>>? OnMarkAsRead { get; set; }

        public Task<Result<IReadOnlyList<NotificationDto>>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false, CancellationToken cancellationToken = default)
        {
            if (OnGetUserNotifications != null) return OnGetUserNotifications(userId, unreadOnly);
            return Task.FromResult(Result.Success<IReadOnlyList<NotificationDto>>([]));
        }

        public Task<Result> MarkAsReadAsync(Guid userId, long notificationId, CancellationToken cancellationToken = default)
        {
            if (OnMarkAsRead != null) return OnMarkAsRead(userId, notificationId);
            return Task.FromResult(Result.Success());
        }

        public Task<Result<NotificationDto>> CreateNotificationAsync(Guid userId, NotificationKind type, string payload, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public string? Email { get; set; } = "test@example.com";
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }

    [Fact]
    public async Task GetNotifications_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var service = new FakeNotificationService();
        var currentUserService = new FakeCurrentUserService { UserId = null };
        var controller = new NotificationsController(service, currentUserService);

        var result = await controller.GetNotifications();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetNotifications_ReturnsOk()
    {
        var currentUserId = Guid.NewGuid();
        var service = new FakeNotificationService
        {
            OnGetUserNotifications = (uid, _) => Task.FromResult(Result.Success<IReadOnlyList<NotificationDto>>([
                new NotificationDto { Id = 1, UserId = uid, Type = NotificationKind.SwapRequest, Payload = "payload" }
            ]))
        };
        var currentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var controller = new NotificationsController(service, currentUserService);

        var result = await controller.GetNotifications();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<NotificationDto>>(ok.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task MarkAsRead_WhenNotFound_ReturnsNotFound()
    {
        var service = new FakeNotificationService
        {
            OnMarkAsRead = (_, _) => Task.FromResult(Result.Failure("Notification with ID 10 was not found."))
        };
        var currentUserService = new FakeCurrentUserService();
        var controller = new NotificationsController(service, currentUserService);

        var result = await controller.MarkAsRead(10, default);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("not found", notFound.Value?.ToString());
    }

    [Fact]
    public async Task MarkAsRead_WhenUnauthorized_ReturnsForbid()
    {
        var service = new FakeNotificationService
        {
            OnMarkAsRead = (_, _) => Task.FromResult(Result.Failure("You are not authorized to access this notification."))
        };
        var currentUserService = new FakeCurrentUserService();
        var controller = new NotificationsController(service, currentUserService);

        var result = await controller.MarkAsRead(10, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task MarkAsRead_WhenSuccessful_ReturnsNoContent()
    {
        var service = new FakeNotificationService();
        var currentUserService = new FakeCurrentUserService();
        var controller = new NotificationsController(service, currentUserService);

        var result = await controller.MarkAsRead(10, default);

        Assert.IsType<NoContentResult>(result);
    }
}
