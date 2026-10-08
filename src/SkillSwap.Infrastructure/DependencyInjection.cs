using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common.Options;
using SkillSwap.Application.Services;
using SkillSwap.Infrastructure.BackgroundJobs;
using SkillSwap.Infrastructure.Identity;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Infrastructure.Services;

namespace SkillSwap.Infrastructure;

/// <summary>
/// Extension method for registering all Infrastructure layer services with the DI container.
/// Called from SkillSwap.API's Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Database ────────────────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found. " +
                "Configure it in appsettings.json or environment variables.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.MigrationsAssembly(
                    typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        // ── Identity ────────────────────────────────────────────────────────
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            // Password policy - tightened for production
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;

            // Lockout
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;

            // User
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // ── Options Configuration ───────────────────────────────────────────
        services.Configure<JwtSettings>(
            configuration.GetSection(JwtSettings.SectionName));

        services.Configure<SessionPolicyOptions>(
            configuration.GetSection(SessionPolicyOptions.SectionName));

        // ── Core Infrastructure Services ────────────────────────────────────
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // ── Locking, Wallet, Quota, Booking & Session Engine ────────────────
        services.AddScoped<IWalletLockService, SqlWalletLockService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<ICreditLedgerService, CreditLedgerService>();
        services.AddScoped<IQuotaService, QuotaService>();
        services.AddScoped<ISessionBookingService, SessionBookingService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<ISessionCompletionService, SessionCompletionService>();
        services.AddScoped<ILedgerReconciliationService, LedgerReconciliationService>();

        // ── Background Jobs ─────────────────────────────────────────────────
        services.AddHostedService<SessionAutoCompletionBackgroundService>();
        services.AddHostedService<PendingSessionExpirationBackgroundService>();

        return services;
    }
}

