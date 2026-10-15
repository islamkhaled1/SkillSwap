using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Skills;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/users/me/skills")]
[Produces("application/json")]
public class UserSkillsController : ControllerBase
{
    private readonly ISkillService _skillService;
    private readonly ICurrentUserService _currentUserService;

    public UserSkillsController(
        ISkillService skillService,
        ICurrentUserService currentUserService)
    {
        _skillService = skillService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Lists all skills declared by the authenticated user (both teach and learn).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserSkillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMySkills(CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _skillService.GetUserSkillsAsync(currentUserId.Value, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Adds a skill declaration (teach or learn) to the authenticated user's profile.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserSkillDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddUserSkill(
        [FromBody] AddUserSkillRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _skillService.AddUserSkillAsync(currentUserId.Value, request, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(result.Error);
            }

            return BadRequest(result.Error);
        }

        return CreatedAtAction(
            nameof(GetMySkills),
            new { id = result.Value!.Id },
            result.Value);
    }

    /// <summary>
    /// Updates the proficiency level of a user's declared skill.
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(UserSkillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserSkill(
        long id,
        [FromBody] UpdateUserSkillRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _skillService.UpdateUserSkillAsync(currentUserId.Value, id, request, cancellationToken);
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
    /// Removes a skill declaration from the authenticated user's profile.
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveUserSkill(
        long id,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _skillService.RemoveUserSkillAsync(currentUserId.Value, id, cancellationToken);
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

        return NoContent();
    }
}
