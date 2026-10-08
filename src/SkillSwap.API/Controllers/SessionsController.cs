using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Sessions;
using SkillSwap.Domain.Enums;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SessionsController : ControllerBase
{
    private readonly ISessionBookingService _bookingService;
    private readonly ISessionService _sessionService;
    private readonly ISessionCompletionService _completionService;
    private readonly ICurrentUserService _currentUserService;

    public SessionsController(
        ISessionBookingService bookingService,
        ISessionService sessionService,
        ISessionCompletionService completionService,
        ICurrentUserService currentUserService)
    {
        _bookingService = bookingService;
        _sessionService = sessionService;
        _completionService = completionService;
        _currentUserService = currentUserService;
    }

    [HttpPost("book")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BookSession([FromBody] BookSessionRequest request, CancellationToken cancellationToken)
    {
        var learnerId = GetCurrentUserId();
        var result = await _bookingService.BookSessionAsync(learnerId, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return CreatedAtAction(nameof(GetSessionById), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionById(long id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _sessionService.GetSessionByIdAsync(id, userId, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(result.Error);

        return Ok(result.Value);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySessions([FromQuery] SessionStatus? status, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _sessionService.GetUserSessionsAsync(userId, status, cancellationToken);
        return Ok(result.Value);
    }

    [HttpPost("{id:long}/confirm")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmSession(long id, CancellationToken cancellationToken)
    {
        var teacherId = GetCurrentUserId();
        var result = await _sessionService.ConfirmSessionAsync(id, teacherId, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return Ok(result.Value);
    }

    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelSession(long id, [FromBody] CancelSessionRequest? request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _sessionService.CancelSessionAsync(id, userId, request?.Reason, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return Ok(result.Value);
    }

    [HttpPost("{id:long}/join")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarkJoined(long id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _sessionService.MarkJoinedAsync(id, userId, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return Ok(result.Value);
    }

    [HttpPost("{id:long}/complete")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CompleteSession(long id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _completionService.CompleteSessionAsync(id, userId, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return Ok(result.Value);
    }

    [HttpPost("{id:long}/no-show")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarkNoShow(long id, [FromBody] MarkNoShowRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _sessionService.MarkNoShowAsync(id, userId, request.Party, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return Ok(result.Value);
    }

    [HttpPost("{id:long}/dispute")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReportDispute(long id, [FromBody] ReportDisputeRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _sessionService.ReportDisputeAsync(id, userId, request.Reason, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return Ok(result.Value);
    }

    private Guid GetCurrentUserId()
    {
        if (_currentUserService.UserId == null)
            throw new UnauthorizedAccessException("User is not authenticated.");

        return _currentUserService.UserId.Value;
    }
}

