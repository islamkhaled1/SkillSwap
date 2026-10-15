using Microsoft.Extensions.Hosting;

namespace SkillSwap.Infrastructure.Identity;

/// <summary>
/// Hosted service that ensures standard User and Admin roles exist upon application startup.
/// </summary>
public sealed class IdentityRoleSeederHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public IdentityRoleSeederHostedService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await IdentityRoleSeeder.SeedRolesAsync(_serviceProvider, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
