using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Wallets;

namespace SkillSwap.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class WalletsController : ControllerBase
{
    private readonly IWalletService _walletService;
    private readonly ICreditLedgerService _ledgerService;
    private readonly IQuotaService _quotaService;
    private readonly ICurrentUserService _currentUserService;

    public WalletsController(
        IWalletService walletService,
        ICreditLedgerService ledgerService,
        IQuotaService quotaService,
        ICurrentUserService currentUserService)
    {
        _walletService = walletService;
        _ledgerService = ledgerService;
        _quotaService = quotaService;
        _currentUserService = currentUserService;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(WalletDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyWallet(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _walletService.GetWalletAsync(userId, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return Ok(result.Value);
    }

    [HttpGet("me/transactions")]
    [ProducesResponseType(typeof(IReadOnlyList<CreditTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyTransactions([FromQuery] int count = 50, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var transactions = await _ledgerService.GetTransactionsByWalletAsync(userId, count, cancellationToken);
        return Ok(transactions);
    }

    [HttpGet("me/quota")]
    [ProducesResponseType(typeof(MonthlyQuotaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyQuota([FromQuery] DateTime? dateUtc, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var targetDate = dateUtc ?? DateTime.UtcNow;
        var quotaResult = await _quotaService.GetMonthlyQuotaAsync(userId, targetDate, cancellationToken);
        if (!quotaResult.IsSuccess)
            return BadRequest(quotaResult.Error);

        return Ok(quotaResult.Value);
    }

    private Guid GetCurrentUserId()
    {
        if (_currentUserService.UserId == null)
            throw new UnauthorizedAccessException("User is not authenticated.");

        return _currentUserService.UserId.Value;
    }
}

