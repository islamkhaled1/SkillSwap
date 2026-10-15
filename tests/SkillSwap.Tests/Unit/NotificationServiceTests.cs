using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class NotificationServiceTests
{
    private (NotificationService Service, ApplicationDbContext Context, TestDateTimeProvider Dtp) CreateTestHarness()
    {
        var context = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString("N"));
        var dtp = new TestDateTimeProvider { UtcNow = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc) };
        var service = new NotificationService(context, dtp);
        return (service, context, dtp);
    }

    [Fact]
    public async Task GetUserNotifications_ReturnsOnlyAuthenticatedUserNotifications()
    {
        var (service, context, dtp) = CreateTestHarness();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        context.Notifications.AddRange(
            new Notification { UserId = user1, Type = NotificationKind.SwapRequest, Payload = "req 1", CreatedAtUtc = dtp.UtcNow.AddMinutes(-5) },
            new Notification { UserId = user1, Type = NotificationKind.NewMessage, Payload = "msg 1", CreatedAtUtc = dtp.UtcNow.AddMinutes(-2) },
            new Notification { UserId = user2, Type = NotificationKind.SwapRequest, Payload = "req 2", CreatedAtUtc = dtp.UtcNow.AddMinutes(-1) }
        );
        await context.SaveChangesAsync();

        var result = await service.GetUserNotificationsAsync(user1);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.All(result.Value, n => Assert.Equal(user1, n.UserId));
        Assert.Equal(NotificationKind.NewMessage, result.Value[0].Type);
    }

    [Fact]
    public async Task GetUserNotifications_WhenUnreadOnly_FiltersOutReadNotifications()
    {
        var (service, context, dtp) = CreateTestHarness();
        var user = Guid.NewGuid();

        context.Notifications.AddRange(
            new Notification { UserId = user, Type = NotificationKind.SwapRequest, Payload = "unread", IsRead = false, CreatedAtUtc = dtp.UtcNow.AddMinutes(-5) },
            new Notification { UserId = user, Type = NotificationKind.NewMessage, Payload = "read", IsRead = true, ReadAtUtc = dtp.UtcNow.AddMinutes(-1), CreatedAtUtc = dtp.UtcNow.AddMinutes(-2) }
        );
        await context.SaveChangesAsync();

        var result = await service.GetUserNotificationsAsync(user, unreadOnly: true);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.False(result.Value![0].IsRead);
    }

    [Fact]
    public async Task MarkAsRead_WhenNotificationNotFound_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();

        var result = await service.MarkAsReadAsync(Guid.NewGuid(), 9999);

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task MarkAsRead_WhenNotificationBelongsToAnotherUser_ReturnsFailure()
    {
        var (service, context, dtp) = CreateTestHarness();
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();

        var notification = new Notification
        {
            UserId = ownerId,
            Type = NotificationKind.SwapRequest,
            Payload = "test",
            IsRead = false,
            CreatedAtUtc = dtp.UtcNow
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var result = await service.MarkAsReadAsync(strangerId, notification.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("not authorized", result.Error);
    }

    [Fact]
    public async Task MarkAsRead_WhenValid_SetsIsReadAndReadAtUtc()
    {
        var (service, context, dtp) = CreateTestHarness();
        var userId = Guid.NewGuid();

        var notification = new Notification
        {
            UserId = userId,
            Type = NotificationKind.SwapRequest,
            Payload = "test",
            IsRead = false,
            CreatedAtUtc = dtp.UtcNow.AddMinutes(-10)
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var result = await service.MarkAsReadAsync(userId, notification.Id);

        Assert.True(result.IsSuccess);

        var updated = await context.Notifications.FirstAsync(n => n.Id == notification.Id);
        Assert.True(updated.IsRead);
        Assert.Equal(dtp.UtcNow, updated.ReadAtUtc);
    }

    [Fact]
    public async Task MarkAsRead_WhenAlreadyRead_IsIdempotentAndSafe()
    {
        var (service, context, dtp) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var originalReadTime = dtp.UtcNow.AddHours(-1);

        var notification = new Notification
        {
            UserId = userId,
            Type = NotificationKind.SwapRequest,
            Payload = "test",
            IsRead = true,
            ReadAtUtc = originalReadTime,
            CreatedAtUtc = dtp.UtcNow.AddHours(-2)
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var result = await service.MarkAsReadAsync(userId, notification.Id);

        Assert.True(result.IsSuccess);

        var updated = await context.Notifications.FirstAsync(n => n.Id == notification.Id);
        Assert.True(updated.IsRead);
        Assert.Equal(originalReadTime, updated.ReadAtUtc);
    }
}
