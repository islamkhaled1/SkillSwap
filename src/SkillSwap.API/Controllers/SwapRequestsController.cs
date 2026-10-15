using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.SwapRequests;
using SkillSwap.Domain.Enums;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/swap-requests")]
[Produces("application/json")]
public class SwapRequestsController : ControllerBase
{
    private readonly ISwapRequestService _swapRequestService;
    private readonly ICurrentUserService _currentUserService;

    public SwapRequestsController(
        ISwapRequestService swapRequestService,
        ICurrentUserService currentUserService)
    {
        _swapRequestService = swapRequestService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Submits a new swap proposal to a teacher.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SwapRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateSwapRequest(
        [FromBody] CreateSwapRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _swapRequestService.CreateSwapRequestAsync(currentUserId.Value, request, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(result.Error);
            }

            return BadRequest(result.Error);
        }

        return CreatedAtAction(
            nameof(GetSwapRequestById),
            new { id = result.Value!.Id },
            result.Value);
    }

    /// <summary>
    /// Lists incoming swap requests where the authenticated user is the teacher/receiver.
    /// </summary>
    [HttpGet("incoming")]
    [ProducesResponseType(typeof(IReadOnlyList<SwapRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetIncomingRequests(
        [FromQuery] SwapRequestStatus? status,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _swapRequestService.GetIncomingRequestsAsync(currentUserId.Value, status, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Lists outgoing swap requests where the authenticated user is the learner/requester.
    /// </summary>
    [HttpGet("outgoing")]
    [ProducesResponseType(typeof(IReadOnlyList<SwapRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOutgoingRequests(
        [FromQuery] SwapRequestStatus? status,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _swapRequestService.GetOutgoingRequestsAsync(currentUserId.Value, status, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Retrieves details of a specific swap request if the caller is a participant.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(SwapRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSwapRequestById(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _swapRequestService.GetSwapRequestByIdAsync(currentUserId.Value, id, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(result.Error);
            }

            if (result.Error?.Contains("not authorized", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Forbid();
            }

            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Accepts a pending swap request (teacher only). Automatically creates a Conversation.
    /// </summary>
    [HttpPost("{id:long}/accept")]
    [ProducesResponseType(typeof(SwapRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptSwapRequest(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _swapRequestService.AcceptSwapRequestAsync(currentUserId.Value, id, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(result.Error);
            }

            if (result.Error?.Contains("Only the receiver", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Forbid();
            }

            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Declines a pending swap request (teacher only).
    /// </summary>
    [HttpPost("{id:long}/decline")]
    [ProducesResponseType(typeof(SwapRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeclineSwapRequest(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _swapRequestService.DeclineSwapRequestAsync(currentUserId.Value, id, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(result.Error);
            }

            if (result.Error?.Contains("Only the receiver", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Forbid();
            }

            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Cancels a pending swap request (learner only).
    /// </summary>
    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(typeof(SwapRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelSwapRequest(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _swapRequestService.CancelSwapRequestAsync(currentUserId.Value, id, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(result.Error);
            }

            if (result.Error?.Contains("Only the requester", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Forbid();
            }

            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }
}
