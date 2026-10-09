
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Skills;

namespace SkillSwap.API.Controllers;

[ApiController]
[Route("api/skills")]
[Produces("application/json")]
public sealed class SkillsController : ControllerBase
{
    private readonly ISkillService _skillService;

    public SkillsController(ISkillService skillService)
    {
        _skillService = skillService;
    }

    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchSkills(
        [FromQuery] string? searchTerm,
        [FromQuery] int? categoryId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _skillService.SearchAsync(
            searchTerm,
            categoryId,
            pageNumber,
            pageSize,
            cancellationToken);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    [ProducesResponseType(
        typeof(SkillDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSkillById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _skillService.GetByIdAsync(
            id,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType(
        typeof(SkillDto),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateSkill(
        [FromBody] CreateSkillRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _skillService.CreateAsync(
            request,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(
            nameof(GetSkillById),
            new { id = result.Value.Id },
            result.Value);
    }
}
