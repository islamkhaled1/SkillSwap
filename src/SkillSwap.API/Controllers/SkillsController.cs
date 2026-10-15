using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Skills;

namespace SkillSwap.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SkillsController : ControllerBase
{
    private readonly ISkillService _skillService;

    public SkillsController(ISkillService skillService)
    {
        _skillService = skillService;
    }

    /// <summary>
    /// Searches and filters active skills by category and keyword.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<SkillDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSkills(
        [FromQuery] int? categoryId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await _skillService.GetActiveSkillsAsync(categoryId, search, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Retrieves an active skill by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SkillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSkillById(int id, CancellationToken cancellationToken)
    {
        var result = await _skillService.GetSkillByIdAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Creates a new catalog skill under a category. Requires Admin role.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(SkillDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateSkill(
        [FromBody] CreateSkillRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _skillService.CreateSkillAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(
            nameof(GetSkillById),
            new { id = result.Value!.Id },
            result.Value);
    }
}
