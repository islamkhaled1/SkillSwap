using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Badges;

namespace SkillSwap.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BadgesController : ControllerBase
{
    private readonly IBadgeService _badgeService;

    public BadgesController(IBadgeService badgeService)
    {
        _badgeService = badgeService;
    }

    /// <summary>
    /// Lists all available badges defined on the platform.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<BadgeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllBadges(CancellationToken cancellationToken)
    {
        var result = await _badgeService.GetBadgesAsync(cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Lists all badges awarded to a specified user.
    /// </summary>
    [HttpGet("/api/users/{id:guid}/badges")]
    [HttpGet("user/{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<UserBadgeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserBadges(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _badgeService.GetUserBadgesAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(result.Value);
    }
}
