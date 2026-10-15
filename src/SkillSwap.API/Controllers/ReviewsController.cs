using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Reviews;

namespace SkillSwap.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ICurrentUserService _currentUserService;

    public ReviewsController(
        IReviewService reviewService,
        ICurrentUserService currentUserService)
    {
        _reviewService = reviewService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Submits a feedback review and star rating for a completed session.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateReview(
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _reviewService.CreateReviewAsync(currentUserId.Value, request, cancellationToken);
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

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>
    /// Retrieves all public reviews received by a user, along with the average rating summary.
    /// Canonical authoritative route: GET /api/users/{id}/reviews
    /// Backward-compatible alias: GET /api/reviews/user/{id}
    /// </summary>
    [HttpGet("/api/users/{id:guid}/reviews")]
    [HttpGet("user/{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserReviewsSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserReviews(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _reviewService.GetUserReviewsAsync(id, cancellationToken);
        return Ok(result.Value);
    }
}
