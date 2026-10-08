using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkillSwap.Application.Abstractions;

namespace SkillSwap.Infrastructure.BackgroundJobs;

public class PendingSessionExpirationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PendingSessionExpirationBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public PendingSessionExpirationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<PendingSessionExpirationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PendingSessionExpirationBackgroundService started.");

        using var timer = new PeriodicTimer(_interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var completionService = scope.ServiceProvider.GetRequiredService<ISessionCompletionService>();
                var expiredCount = await completionService.ExpirePendingSessionsAsync(stoppingToken);
                if (expiredCount > 0)
                {
                    _logger.LogInformation("Expired {Count} pending confirmation sessions.", expiredCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error occurred during pending session expiration background execution.");
            }
        }
    }
}
