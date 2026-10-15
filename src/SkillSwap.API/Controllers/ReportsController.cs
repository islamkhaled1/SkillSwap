using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Reports;
using SkillSwap.Domain.Enums;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ICurrentUserService _currentUserService;

    public ReportsController(
        IReportService reportService,
        ICurrentUserService currentUserService)
    {
        _reportService = reportService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Submits a moderation report against another user.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReportDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateReport(
        [FromBody] CreateReportRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var result = await _reportService.CreateReportAsync(currentUserId.Value, request, cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(result.Error);
            }

            return BadRequest(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    /// <summary>
    /// Administrative moderation: lists submitted reports, optionally filtered by status.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReports(
        [FromQuery] ReportStatus? status,
        CancellationToken cancellationToken)
    {
        var result = await _reportService.GetReportsAsync(status, cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>
    /// Administrative moderation: updates the review status of a report.
    /// </summary>
    [HttpPut("{id:long}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReportStatus(
        long id,
        [FromBody] UpdateReportStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _reportService.UpdateReportStatusAsync(id, request.Status, cancellationToken);
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
