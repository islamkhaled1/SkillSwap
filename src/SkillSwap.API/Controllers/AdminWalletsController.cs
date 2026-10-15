using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Wallets;

namespace SkillSwap.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/wallets")]
[Produces("application/json")]
public class AdminWalletsController : ControllerBase
{
    private readonly IWalletService _walletService;
    private readonly IApplicationDbContext _context;

    public AdminWalletsController(
        IWalletService walletService,
        IApplicationDbContext context)
    {
        _walletService = walletService;
        _context = context;
    }

    /// <summary>
    /// Administrative endpoint: grants initial or promotional bonus learning credits to a user's wallet.
    /// Reuses the existing financial engine and records an audited CreditTransaction of type Bonus.
    /// </summary>
    [HttpPost("{userId:guid}/bonus")]
    [ProducesResponseType(typeof(CreditTransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GrantBonus(
        Guid userId,
        [FromBody] GrantBonusRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest("Idempotency-Key header is required and cannot be empty.");

        var trimmedKey = idempotencyKey.Trim();
        if (trimmedKey.Length > 100)
            return BadRequest("Idempotency-Key header cannot exceed 100 characters.");

        if (request == null)
            return BadRequest("Request payload cannot be null.");

        var userExists = await _context.UserProfiles
            .AsNoTracking()
            .AnyAsync(u => u.UserId == userId, cancellationToken);

        if (!userExists)
            return NotFound($"User with ID '{userId}' was not found.");

        var result = await _walletService.ApplyBonusAsync(
            userId,
            request.Minutes,
            request.Description,
            trimmedKey,
            cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error?.Contains("conflict", StringComparison.OrdinalIgnoreCase) == true)
                return Conflict(result.Error);

            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }
}
