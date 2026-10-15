using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Chat;

namespace SkillSwap.API.Hubs;

/// <summary>
/// SignalR hub handling real-time messaging between conversation participants.
/// Mapped to /hubs/chat in Program.cs.
/// </summary>
[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IChatService chatService,
        ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("ChatHub client connected: {ConnectionId}, User: {UserId}",
            Context.ConnectionId, GetCurrentUserId());
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "ChatHub client disconnected: {ConnectionId}. Reason: {Reason}",
            Context.ConnectionId,
            exception?.Message ?? "clean disconnect");
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Joins a conversation's real-time messaging group after verifying membership.
    /// </summary>
    public async Task JoinConversation(long conversationId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new HubException("User identity could not be verified.");
        }

        var isParticipant = await _chatService.IsParticipantAsync(userId.Value, conversationId);
        if (!isParticipant)
        {
            _logger.LogWarning("User {UserId} attempted to join unauthorized conversation {ConversationId}",
                userId.Value, conversationId);
            throw new HubException("You are not a participant in this conversation.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        _logger.LogInformation("User {UserId} joined group conversation_{ConversationId}",
            userId.Value, conversationId);
    }

    /// <summary>
    /// Leaves a conversation's real-time messaging group.
    /// </summary>
    public async Task LeaveConversation(long conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
    }

    /// <summary>
    /// Persists a chat message and broadcasts it to group participants.
    /// </summary>
    public async Task<MessageDto> SendMessage(long conversationId, string body)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new HubException("User identity could not be verified.");
        }

        var result = await _chatService.SendMessageAsync(userId.Value, conversationId, body);
        if (!result.IsSuccess)
        {
            throw new HubException(result.Error);
        }

        var messageDto = result.Value!;

        // Broadcast to all clients in the conversation group
        await Clients.Group($"conversation_{conversationId}").SendAsync("ReceiveMessage", messageDto);

        return messageDto;
    }

    private Guid? GetCurrentUserId()
    {
        var idClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        return Guid.TryParse(idClaim, out var guid) ? guid : null;
    }
}
