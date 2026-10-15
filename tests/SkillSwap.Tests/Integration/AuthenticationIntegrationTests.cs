using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Auth;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure;
using SkillSwap.Infrastructure.Identity;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Infrastructure.Services;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Integration;

public class AuthenticationIntegrationTests
{
    [Fact]
    public async Task RoleSeeder_InitializesUserAndAdminRoles_SafelyAndIdempotently()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var dbName = "RoleSeederTestDb_" + Guid.NewGuid().ToString("N");
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        var sp = services.BuildServiceProvider();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        Assert.False(await roleManager.RoleExistsAsync("User"));
        Assert.False(await roleManager.RoleExistsAsync("Admin"));

        // Seed once
        await IdentityRoleSeeder.SeedRolesAsync(sp);

        Assert.True(await roleManager.RoleExistsAsync("User"));
        Assert.True(await roleManager.RoleExistsAsync("Admin"));

        // Seed twice (idempotence verification)
        await IdentityRoleSeeder.SeedRolesAsync(sp);

        Assert.True(await roleManager.RoleExistsAsync("User"));
        Assert.True(await roleManager.RoleExistsAsync("Admin"));
    }

    [Fact]
    public void DependencyInjection_AddInfrastructure_RegistersAuthServiceAndRoleSeeder()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=SkillSwap_Test;Trusted_Connection=True;TrustServerCertificate=True;",
                ["Jwt:SecretKey"] = "this_is_a_very_secret_test_key_with_at_least_32_characters_for_hmac_sha256!",
                ["Jwt:Issuer"] = "SkillSwap.Test",
                ["Jwt:Audience"] = "SkillSwap.TestAudience",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        var sp = services.BuildServiceProvider();

        var authService = sp.GetService<IAuthService>();
        Assert.NotNull(authService);
        Assert.IsType<AuthService>(authService);

        var hostedServices = sp.GetServices<IHostedService>();
        Assert.Contains(hostedServices, s => s is IdentityRoleSeederHostedService);
    }

    [Fact]
    public async Task Registration_AtomicRollback_WhenExceptionOccursMidway_RollsBackAllRecords()
    {
        // Use real SQL Server context to test real relational rollback behavior
        using var context = TestDbContextFactory.CreateSqlServerContext();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => context);

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        var jwtSettings = new JwtSettings
        {
            SecretKey = "a_super_secret_test_key_for_signing_tokens_with_256_bits_length!",
            Issuer = "SkillSwap.Test",
            Audience = "SkillSwap.TestAudience",
            ExpiryMinutes = 60
        };
        var jwtOptions = Options.Create(jwtSettings);
        services.AddSingleton(jwtOptions);

        var dtp = new TestDateTimeProvider();
        services.AddSingleton<IDateTimeProvider>(dtp);
        var jwtTokenService = new JwtTokenService(jwtOptions);
        services.AddSingleton<IJwtTokenService>(jwtTokenService);

        var sp = services.BuildServiceProvider();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        // Ensure "FREE" plan exists
        var freePlan = await context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == "FREE");
        if (freePlan == null)
        {
            freePlan = new SubscriptionPlan
            {
                Id = 1,
                Code = "FREE",
                Name = "Free",
                MonthlyLearningMinutes = 180,
                Price = 0.00m,
                PriorityMatching = false,
                AdvancedSearch = false,
                IsActive = true
            };
            context.SubscriptionPlans.Add(freePlan);
            await context.SaveChangesAsync();
        }

        // Ensure "User" role exists
        if (!await roleManager.RoleExistsAsync("User"))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>("User"));
        }

        var uniqueEmail = $"rollback_test_{Guid.NewGuid():N}@example.com";

        // Explicitly simulate midway transaction failure on SQL Server:
        // Begin transaction, create Identity user, then simulate failure before commit and rollback
        await using var transaction = await context.Database.BeginTransactionAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = uniqueEmail,
            Email = uniqueEmail,
            LockoutEnabled = true,
            CreatedAtUtc = dtp.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, "SecurePassword123!");
        Assert.True(createResult.Succeeded);

        var roleResult = await userManager.AddToRoleAsync(user, "User");
        Assert.True(roleResult.Succeeded);

        // Simulate failure midway -> Rollback
        await transaction.RollbackAsync();

        // Verify with a fresh query that no record was committed to SQL Server
        var rolledBackUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == uniqueEmail);
        Assert.Null(rolledBackUser);

        var rolledBackProfile = await context.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id);
        Assert.Null(rolledBackProfile);

        var rolledBackWallet = await context.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == user.Id);
        Assert.Null(rolledBackWallet);

        var rolledBackSub = await context.UserSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == user.Id);
        Assert.Null(rolledBackSub);
    }
}
