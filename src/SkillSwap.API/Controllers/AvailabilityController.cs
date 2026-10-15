using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Availability;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/users/me/availability")]
[Produces("application/json")]
public class AvailabilityController : ControllerBase
{
    private readonly IAvailabilityService _availabilityService;
    private readonly ICurrentUserService _currentUserService;

    public AvailabilityController(
        IAvailabilityService availabilityService,
        ICurrentUserService currentUserService)
    {
        _availabilityService = availabilityService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Retrieves the weekly recurring availability schedule for the authenticated user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AvailabilitySlotDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyAvailability(CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _availabilityService.GetUserAvailabilityAsync(currentUserId.Value, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Replaces the weekly recurring availability schedule for the authenticated user.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(IReadOnlyList<AvailabilitySlotDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMyAvailability(
        [FromBody] UpdateAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _availabilityService.UpdateUserAvailabilityAsync(currentUserId.Value, request, cancellationToken);
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
