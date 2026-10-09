
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Skills;

namespace SkillSwap.API.Controllers;

[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<CategoryDto>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(
        CancellationToken cancellationToken)
    {
        var categories = await _categoryService.GetActiveAsync(
            cancellationToken);

        return Ok(categories);
    }
}
