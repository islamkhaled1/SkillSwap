using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SkillSwap.API.Controllers;
using SkillSwap.API.Hubs;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Chat;

namespace SkillSwap.Tests.Unit;

public class ConversationsControllerTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public string? Email { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }

    private class FakeClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private class FakeHubClients : IHubClients
    {
        public IClientProxy All => new FakeClientProxy();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
        public IClientProxy Client(string connectionId) => new FakeClientProxy();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new FakeClientProxy();
        public IClientProxy Group(string groupName) => new FakeClientProxy();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeClientProxy();
        public IClientProxy User(string userId) => new FakeClientProxy();
        public IClientProxy Users(IReadOnlyList<string> userIds) => new FakeClientProxy();
    }

    private class FakeGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private class FakeHubContext : IHubContext<ChatHub>
    {
        public IHubClients Clients { get; } = new FakeHubClients();
        public IGroupManager Groups { get; } = new FakeGroupManager();
    }

    private class FakeChatService : IChatService
    {
        public Func<Guid, Task<Result<IReadOnlyList<ConversationDto>>>>? OnGetUserConversations { get; set; }
        public Func<Guid, long, Task<Result<ConversationDto>>>? OnGetConversationById { get; set; }
        public Func<Guid, long, int, int, Task<Result<IReadOnlyList<MessageDto>>>>? OnGetConversationMessages { get; set; }
        public Func<Guid, long, string, Task<Result<MessageDto>>>? OnSendMessage { get; set; }
        public Func<Guid, long, long, Task<Result>>? OnMarkConversationAsRead { get; set; }

        public Task<Result<IReadOnlyList<ConversationDto>>> GetUserConversationsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (OnGetUserConversations != null)
                return OnGetUserConversations(userId);

            return Task.FromResult(Result.Success<IReadOnlyList<ConversationDto>>([
                new ConversationDto { Id = 1, SwapRequestId = 10, OtherParticipantId = Guid.NewGuid(), OtherParticipantDisplayName = "Bob" }
            ]));
        }

        public Task<Result<ConversationDto>> GetConversationByIdAsync(Guid userId, long conversationId, CancellationToken cancellationToken = default)
        {
            if (OnGetConversationById != null)
                return OnGetConversationById(userId, conversationId);

            return Task.FromResult(Result.Success(new ConversationDto
            {
                Id = conversationId,
                SwapRequestId = 10,
                OtherParticipantId = Guid.NewGuid(),
                OtherParticipantDisplayName = "Bob"
            }));
        }

        public Task<Result<IReadOnlyList<MessageDto>>> GetConversationMessagesAsync(Guid userId, long conversationId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        {
            if (OnGetConversationMessages != null)
                return OnGetConversationMessages(userId, conversationId, page, pageSize);

            return Task.FromResult(Result.Success<IReadOnlyList<MessageDto>>([
                new MessageDto { Id = 1, ConversationId = conversationId, SenderId = userId, Body = "Hi" }
            ]));
        }

        public Task<Result<MessageDto>> SendMessageAsync(Guid senderId, long conversationId, string body, CancellationToken cancellationToken = default)
        {
            if (OnSendMessage != null)
                return OnSendMessage(senderId, conversationId, body);

            return Task.FromResult(Result.Success(new MessageDto
            {
                Id = 2,
                ConversationId = conversationId,
                SenderId = senderId,
                Body = body
            }));
        }

        public Task<Result> MarkConversationAsReadAsync(Guid userId, long conversationId, long lastReadMessageId, CancellationToken cancellationToken = default)
        {
            if (OnMarkConversationAsRead != null)
                return OnMarkConversationAsRead(userId, conversationId, lastReadMessageId);

            return Task.FromResult(Result.Success());
        }

        public Task<bool> IsParticipantAsync(Guid userId, long conversationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    [Fact]
    public async Task GetMyConversations_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = null };
        var controller = new ConversationsController(new FakeChatService(), fakeCurrentUserService, new FakeHubContext());

        var response = await controller.GetMyConversations(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
    }

    [Fact]
    public async Task GetMyConversations_WhenAuthenticated_ReturnsOkWithList()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var controller = new ConversationsController(new FakeChatService(), fakeCurrentUserService, new FakeHubContext());

        var response = await controller.GetMyConversations(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        var list = Assert.IsAssignableFrom<IReadOnlyList<ConversationDto>>(ok.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task GetConversationById_WhenForbidden_ReturnsForbid()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var fakeChatService = new FakeChatService
        {
            OnGetConversationById = (_, _) => Task.FromResult(Result.Failure<ConversationDto>("You are not a participant in this conversation."))
        };
        var controller = new ConversationsController(fakeChatService, fakeCurrentUserService, new FakeHubContext());

        var response = await controller.GetConversationById(1, CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
    }

    [Fact]
    public async Task SendMessage_ValidMessage_ReturnsCreated()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var controller = new ConversationsController(new FakeChatService(), fakeCurrentUserService, new FakeHubContext());

        var response = await controller.SendMessage(1, new SendMessageRequest { Body = "Hello!" }, CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var dto = Assert.IsType<MessageDto>(created.Value);
        Assert.Equal("Hello!", dto.Body);
    }

    [Fact]
    public async Task SendMessage_WhenNonParticipant_ReturnsForbid()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var fakeChatService = new FakeChatService
        {
            OnSendMessage = (_, _, _) => Task.FromResult(Result.Failure<MessageDto>("You are not a participant in this conversation."))
        };
        var controller = new ConversationsController(fakeChatService, fakeCurrentUserService, new FakeHubContext());

        var response = await controller.SendMessage(1, new SendMessageRequest { Body = "Hello!" }, CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
    }

    [Fact]
    public async Task MarkAsRead_WhenSuccess_ReturnsNoContent()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var controller = new ConversationsController(new FakeChatService(), fakeCurrentUserService, new FakeHubContext());

        var response = await controller.MarkAsRead(1, new MarkConversationReadRequest { LastReadMessageId = 5 }, CancellationToken.None);

        Assert.IsType<NoContentResult>(response);
    }
}
