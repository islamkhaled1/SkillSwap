using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Blocks;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BlocksController : ControllerBase
{
    private readonly IBlockService _blockService;
    private readonly ICurrentUserService _currentUserService;

    public BlocksController(
        IBlockService blockService,
        ICurrentUserService currentUserService)
    {
        _blockService = blockService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Lists all users blocked by the authenticated caller.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BlockedUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBlockedUsers(CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _blockService.GetBlockedUsersAsync(currentUserId.Value, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Blocks another user from swaps, matching, and chat messaging.
    /// </summary>
    [HttpPost("{userId:guid}")]
    [ProducesResponseType(typeof(BlockedUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BlockUser(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _blockService.BlockUserAsync(currentUserId.Value, userId, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(result.Error);
            }

            if (result.Error?.Contains("already", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Conflict(result.Error);
            }

            return BadRequest(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>
    /// Unblocks a previously blocked user.
    /// </summary>
    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnblockUser(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _blockService.UnblockUserAsync(currentUserId.Value, userId, cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return NoContent();
    }
}
