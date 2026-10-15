using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SkillSwap.API.Hubs;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Chat;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ConversationsController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHubContext<ChatHub> _hubContext;

    public ConversationsController(
        IChatService chatService,
        ICurrentUserService currentUserService,
        IHubContext<ChatHub> hubContext)
    {
        _chatService = chatService;
        _currentUserService = currentUserService;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Lists all conversations where the authenticated user is a participant.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyConversations(CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _chatService.GetUserConversationsAsync(currentUserId.Value, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Retrieves a specific conversation's metadata by ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversationById(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _chatService.GetConversationByIdAsync(currentUserId.Value, id, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(result.Error);
            }

            if (result.Error?.Contains("not a participant", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Forbid();
            }

            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Retrieves paginated message history for a conversation.
    /// </summary>
    [HttpGet("{id:long}/messages")]
    [ProducesResponseType(typeof(IReadOnlyList<MessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(
        long id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _chatService.GetConversationMessagesAsync(currentUserId.Value, id, page, pageSize, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(result.Error);
            }

            if (result.Error?.Contains("not a participant", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Forbid();
            }

            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Sends a new message in a conversation via REST API (fallback to SignalR).
    /// </summary>
    [HttpPost("{id:long}/messages")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(
        long id,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _chatService.SendMessageAsync(currentUserId.Value, id, request.Body, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(result.Error);
            }

            if (result.Error?.Contains("not a participant", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Forbid();
            }

            return BadRequest(result.Error);
        }

        // Broadcast to SignalR clients in the conversation group
        await _hubContext.Clients.Group($"conversation_{id}").SendAsync("ReceiveMessage", result.Value, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>
    /// Updates the user's read cursor (LastReadMessageId) for a conversation.
    /// </summary>
    [HttpPost("{id:long}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(
        long id,
        [FromBody] MarkConversationReadRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _chatService.MarkConversationAsReadAsync(currentUserId.Value, id, request.LastReadMessageId, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(result.Error);
            }

            if (result.Error?.Contains("not a participant", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Forbid();
            }

            return BadRequest(result.Error);
        }

        return NoContent();
    }
}
