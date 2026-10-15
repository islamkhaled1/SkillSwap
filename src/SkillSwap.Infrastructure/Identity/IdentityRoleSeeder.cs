using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SkillSwap.Infrastructure.Identity;

/// <summary>
/// Seeds standard application roles safely during initialization without creating user accounts.
/// </summary>
public static class IdentityRoleSeeder
{
    public static readonly string[] StandardRoles = ["User", "Admin"];

    public static async Task SeedRolesAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger(typeof(IdentityRoleSeeder));

        foreach (var roleName in StandardRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var role = new IdentityRole<Guid>(roleName);
                var result = await roleManager.CreateAsync(role);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger?.LogError("Failed to seed Identity role {Role}: {Errors}", roleName, errors);
                    throw new InvalidOperationException($"Failed to seed Identity role '{roleName}': {errors}");
                }
                logger?.LogInformation("Seeded Identity role: {Role}", roleName);
            }
        }

        // Provision initial administrator if explicitly configured via environment or configuration
        var configuration = scope.ServiceProvider.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
        var adminEmail = configuration?["AdminUser:Email"];
        var adminPassword = configuration?["AdminUser:Password"];
        var adminDisplayName = configuration?["AdminUser:DisplayName"] ?? "Platform Administrator";

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var userManager = scope.ServiceProvider.GetService<UserManager<ApplicationUser>>();
            var context = scope.ServiceProvider.GetService<Persistence.ApplicationDbContext>();

            if (userManager != null && context != null)
            {
                var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
                if (existingAdmin == null)
                {
                    var adminUser = new ApplicationUser
                    {
                        Id = Guid.NewGuid(),
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true
                    };

                    var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                    if (createResult.Succeeded)
                    {
                        await userManager.AddToRoleAsync(adminUser, "User");
                        await userManager.AddToRoleAsync(adminUser, "Admin");

                        context.UserProfiles.Add(new Domain.Entities.UserProfile
                        {
                            UserId = adminUser.Id,
                            DisplayName = adminDisplayName,
                            TimeZoneId = "UTC",
                            IsVerified = true,
                            CreatedAtUtc = DateTime.UtcNow
                        });

                        context.Wallets.Add(new Domain.Entities.Wallet
                        {
                            UserId = adminUser.Id,
                            AvailableMinutes = 0,
                            HeldMinutes = 0,
                            UpdatedAtUtc = DateTime.UtcNow
                        });

                        var freePlan = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                            context.SubscriptionPlans,
                            p => p.Code == "FREE",
                            cancellationToken);

                        if (freePlan != null)
                        {
                            context.UserSubscriptions.Add(new Domain.Entities.UserSubscription
                            {
                                UserId = adminUser.Id,
                                PlanId = freePlan.Id,
                                Status = Domain.Enums.SubscriptionStatus.Active,
                                StartsAtUtc = DateTime.UtcNow
                            });
                        }

                        await context.SaveChangesAsync(cancellationToken);
                        logger?.LogInformation("Provisioned initial administrator account for {Email}", adminEmail);
                    }
                    else
                    {
                        var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                        logger?.LogWarning("Failed to provision initial administrator {Email}: {Errors}", adminEmail, errors);
                    }
                }
                else
                {
                    if (!await userManager.IsInRoleAsync(existingAdmin, "Admin"))
                    {
                        await userManager.AddToRoleAsync(existingAdmin, "Admin");
                        logger?.LogInformation("Ensured Admin role on existing account {Email}", adminEmail);
                    }
                }
            }
        }
    }
}
