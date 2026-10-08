using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Sessions;

namespace SkillSwap.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/sessions")]
[Produces("application/json")]
public class AdminSessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly ICurrentUserService _currentUserService;

    public AdminSessionsController(
        ISessionService sessionService,
        ICurrentUserService currentUserService)
    {
        _sessionService = sessionService;
        _currentUserService = currentUserService;
    }

    [HttpPost("{id:long}/resolve-dispute")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveDispute(
        long id,
        [FromBody] ResolveDisputeRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId == null)
            return Unauthorized();

        var adminId = _currentUserService.UserId.Value;
        var result = await _sessionService.ResolveDisputeAsync(
            id,
            adminId,
            request.AwardTeacher,
            request.ResolutionNote,
            cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return Ok(result.Value);
    }
}
