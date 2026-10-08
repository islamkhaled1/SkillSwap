using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SkillSwap.API.Hubs;

/// <summary>
/// Placeholder SignalR hub for real-time chat between session participants.
///
/// FOUNDATION ONLY - business logic NOT implemented yet.
/// The chat feature requires:
///   - Message persistence (Message entity per DATABASE.md)
///   - Conversation / ConversationParticipant validation
///   - User authorization checks
///
/// Future implementations should inject IMediator or a dedicated chat service.
/// </summary>
[Authorize]
public sealed class ChatHub : Hub
{
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ILogger<ChatHub> logger)
    {
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "Client disconnected: {ConnectionId}. Reason: {Reason}",
            Context.ConnectionId,
            exception?.Message ?? "clean disconnect");
        return base.OnDisconnectedAsync(exception);
    }

    // TODO: Implement SendMessage(string conversationId, string content)
    // TODO: Implement JoinConversation(string conversationId)
    // TODO: Implement LeaveConversation(string conversationId)
}
