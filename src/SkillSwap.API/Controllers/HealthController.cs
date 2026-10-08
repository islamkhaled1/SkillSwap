using Microsoft.AspNetCore.Mvc;

namespace SkillSwap.API.Controllers;

/// <summary>
/// Minimal health check endpoint to verify the API is running.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns a simple alive check.
    /// </summary>
    /// <returns>200 OK with status information.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        _logger.LogDebug("Health check called");

        return Ok(new HealthResponse(
            Status: "Healthy",
            Timestamp: DateTime.UtcNow,
            Version: "1.0.0"
        ));
    }
}

/// <summary>
/// Health check response payload.
/// </summary>
public sealed record HealthResponse(string Status, DateTime Timestamp, string Version);
