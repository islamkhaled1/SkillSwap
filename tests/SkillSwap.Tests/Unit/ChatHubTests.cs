using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using SkillSwap.API.Hubs;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Chat;

namespace SkillSwap.Tests.Unit;

public class ChatHubTests
{
    private class FakeClientProxy : IClientProxy
    {
        public string? LastMethod { get; private set; }
        public object?[]? LastArgs { get; private set; }

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            LastMethod = method;
            LastArgs = args;
            return Task.CompletedTask;
        }
    }

    private class FakeHubCallerClients : IHubCallerClients
    {
        public FakeClientProxy GroupProxy { get; } = new();

        public IClientProxy All => new FakeClientProxy();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
        public IClientProxy Client(string connectionId) => new FakeClientProxy();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new FakeClientProxy();
        public IClientProxy Group(string groupName) => GroupProxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeClientProxy();
        public IClientProxy User(string userId) => new FakeClientProxy();
        public IClientProxy Users(IReadOnlyList<string> userIds) => new FakeClientProxy();
        public IClientProxy Caller => new FakeClientProxy();
        public IClientProxy Others => new FakeClientProxy();
        public IClientProxy OthersInGroup(string groupName) => new FakeClientProxy();
        public IClientProxy OthersInGroups(IReadOnlyList<string> groupNames) => new FakeClientProxy();
    }

    private class FakeGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> AddedGroups { get; } = [];
        public List<(string ConnectionId, string GroupName)> RemovedGroups { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            AddedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            RemovedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private class FakeHubCallerContext : HubCallerContext
    {
        private readonly ClaimsPrincipal _user;
        public FakeHubCallerContext(string connectionId, ClaimsPrincipal user)
        {
            ConnectionId = connectionId;
            _user = user;
        }
        public override string ConnectionId { get; }
        public override string? UserIdentifier => _user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        public override ClaimsPrincipal? User => _user;
        public override IDictionary<object, object?> Items => new Dictionary<object, object?>();
        public override IFeatureCollection Features => new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }

    private class FakeChatService : IChatService
    {
        public Func<Guid, long, Task<bool>>? OnIsParticipant { get; set; }
        public Func<Guid, long, string, Task<Result<MessageDto>>>? OnSendMessage { get; set; }

        public Task<Result<IReadOnlyList<ConversationDto>>> GetUserConversationsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<ConversationDto>> GetConversationByIdAsync(Guid userId, long conversationId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<IReadOnlyList<MessageDto>>> GetConversationMessagesAsync(Guid userId, long conversationId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<MessageDto>> SendMessageAsync(Guid senderId, long conversationId, string body, CancellationToken cancellationToken = default)
        {
            if (OnSendMessage != null)
                return OnSendMessage(senderId, conversationId, body);

            return Task.FromResult(Result.Success(new MessageDto
            {
                Id = 10,
                ConversationId = conversationId,
                SenderId = senderId,
                Body = body,
                SentAtUtc = DateTime.UtcNow
            }));
        }

        public Task<Result> MarkConversationAsReadAsync(Guid userId, long conversationId, long lastReadMessageId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> IsParticipantAsync(Guid userId, long conversationId, CancellationToken cancellationToken = default)
        {
            if (OnIsParticipant != null)
                return OnIsParticipant(userId, conversationId);

            return Task.FromResult(true);
        }
    }

    private static (ChatHub Hub, FakeGroupManager Groups, FakeHubCallerClients Clients) CreateHarness(Guid userId, FakeChatService chatService)
    {
        var hub = new ChatHub(chatService, NullLogger<ChatHub>.Instance);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var groupManager = new FakeGroupManager();
        var clients = new FakeHubCallerClients();

        hub.Context = new FakeHubCallerContext("conn_123", principal);
        hub.Groups = groupManager;
        hub.Clients = clients;

        return (hub, groupManager, clients);
    }

    [Fact]
    public async Task JoinConversation_AuthorizedParticipant_AddsToSignalRGroup()
    {
        var userId = Guid.NewGuid();
        var fakeService = new FakeChatService { OnIsParticipant = (_, _) => Task.FromResult(true) };
        var (hub, groups, _) = CreateHarness(userId, fakeService);

        await hub.JoinConversation(42);

        Assert.Single(groups.AddedGroups);
        Assert.Equal("conn_123", groups.AddedGroups[0].ConnectionId);
        Assert.Equal("conversation_42", groups.AddedGroups[0].GroupName);
    }

    [Fact]
    public async Task JoinConversation_UnauthorizedNonParticipant_ThrowsHubException()
    {
        var userId = Guid.NewGuid();
        var fakeService = new FakeChatService { OnIsParticipant = (_, _) => Task.FromResult(false) };
        var (hub, groups, _) = CreateHarness(userId, fakeService);

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinConversation(42));
        Assert.Contains("not a participant", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(groups.AddedGroups);
    }

    [Fact]
    public async Task SendMessage_Authorized_PersistsAndBroadcastsToGroup()
    {
        var userId = Guid.NewGuid();
        var fakeService = new FakeChatService();
        var (hub, _, clients) = CreateHarness(userId, fakeService);

        var messageDto = await hub.SendMessage(42, "Hello live chat!");

        Assert.NotNull(messageDto);
        Assert.Equal("Hello live chat!", messageDto.Body);
        Assert.Equal("ReceiveMessage", clients.GroupProxy.LastMethod);
        Assert.NotNull(clients.GroupProxy.LastArgs);
        Assert.Equal(messageDto, clients.GroupProxy.LastArgs[0]);
    }

    [Fact]
    public async Task SendMessage_WhenServiceFails_ThrowsHubException()
    {
        var userId = Guid.NewGuid();
        var fakeService = new FakeChatService
        {
            OnSendMessage = (_, _, _) => Task.FromResult(Result.Failure<MessageDto>("Cannot send message due to block restrictions."))
        };
        var (hub, _, clients) = CreateHarness(userId, fakeService);

        var ex = await Assert.ThrowsAsync<HubException>(() => hub.SendMessage(42, "Blocked"));
        Assert.Contains("block", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(clients.GroupProxy.LastMethod);
    }

    [Fact]
    public async Task LeaveConversation_RemovesFromSignalRGroup()
    {
        var userId = Guid.NewGuid();
        var fakeService = new FakeChatService();
        var (hub, groups, _) = CreateHarness(userId, fakeService);

        await hub.LeaveConversation(42);

        Assert.Single(groups.RemovedGroups);
        Assert.Equal("conn_123", groups.RemovedGroups[0].ConnectionId);
        Assert.Equal("conversation_42", groups.RemovedGroups[0].GroupName);
    }
}
