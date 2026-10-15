using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SkillSwap.Infrastructure.Services;

namespace SkillSwap.API.Extensions;

/// <summary>
/// JWT Bearer authentication configuration.
/// </summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddSkillSwapAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? new JwtSettings();

        // Support fallback to Jwt:Key or environment variable Jwt__Key if not bound via section
        if (string.IsNullOrWhiteSpace(jwtSettings.SigningKey))
        {
            var fallbackKey = configuration["Jwt:Key"]
                ?? configuration["Jwt__Key"]
                ?? configuration["Jwt:SecretKey"]
                ?? configuration["Jwt__SecretKey"];

            if (!string.IsNullOrWhiteSpace(fallbackKey))
            {
                jwtSettings.Key = fallbackKey;
            }
        }

        var isProduction = environment?.IsProduction() ??
            string.Equals(
                configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"],
                "Production",
                StringComparison.OrdinalIgnoreCase);

        JwtSettingsValidator.Validate(jwtSettings, isProduction);

        var key = Encoding.UTF8.GetBytes(jwtSettings.SigningKey);

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = jwtSettings.Issuer,
                    ValidAudience            = jwtSettings.Audience,
                    IssuerSigningKey         = new SymmetricSecurityKey(key),
                    ClockSkew                = TimeSpan.Zero
                };

                // Allow JWT via query string for SignalR connections
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var accessToken = ctx.Request.Query["access_token"];
                        var path = ctx.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs"))
                        {
                            ctx.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }
}
