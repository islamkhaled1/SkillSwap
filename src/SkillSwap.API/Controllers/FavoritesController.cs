using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Favorites;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FavoritesController : ControllerBase
{
    private readonly IFavoriteService _favoriteService;
    private readonly ICurrentUserService _currentUserService;

    public FavoritesController(
        IFavoriteService favoriteService,
        ICurrentUserService currentUserService)
    {
        _favoriteService = favoriteService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Lists all bookmarked favorite users for the authenticated caller.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FavoriteUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFavorites(CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _favoriteService.GetFavoritesAsync(currentUserId.Value, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Adds another user to the authenticated caller's favorites list.
    /// </summary>
    [HttpPost("{userId:guid}")]
    [ProducesResponseType(typeof(FavoriteUserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddFavorite(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _favoriteService.AddFavoriteAsync(currentUserId.Value, userId, cancellationToken);
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
    /// Removes a user from the authenticated caller's favorites list.
    /// </summary>
    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFavorite(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _favoriteService.RemoveFavoriteAsync(currentUserId.Value, userId, cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return NoContent();
    }
}
