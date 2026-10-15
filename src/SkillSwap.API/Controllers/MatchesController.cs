using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Matching;
using SkillSwap.Domain.Enums;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MatchesController : ControllerBase
{
    private readonly IMatchingService _matchingService;
    private readonly ICurrentUserService _currentUserService;

    public MatchesController(
        IMatchingService matchingService,
        ICurrentUserService currentUserService)
    {
        _matchingService = matchingService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Computes and retrieves peer match recommendations based on the user's learn/teach profile and availability.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MatchRecommendationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMatches(
        [FromQuery] int? skillId,
        [FromQuery] UserSkillLevel? minLevel,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _matchingService.GetMatchesAsync(currentUserId.Value, skillId, minLevel, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(result.Error);
            }

            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }
}
