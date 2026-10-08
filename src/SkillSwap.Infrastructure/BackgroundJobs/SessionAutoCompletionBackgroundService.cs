using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SkillSwap.Application.Abstractions;

namespace SkillSwap.Infrastructure.BackgroundJobs;

public class SessionAutoCompletionBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionAutoCompletionBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    public SessionAutoCompletionBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SessionAutoCompletionBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SessionAutoCompletionBackgroundService started.");

        using var timer = new PeriodicTimer(_interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var completionService = scope.ServiceProvider.GetRequiredService<ISessionCompletionService>();
                var completedCount = await completionService.AutoCompleteEligibleSessionsAsync(stoppingToken);
                if (completedCount > 0)
                {
                    _logger.LogInformation("Auto-completed {Count} eligible sessions.", completedCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error occurred during session auto-completion background execution.");
            }
        }
    }
}
