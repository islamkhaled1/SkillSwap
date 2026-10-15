using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SkillSwap.Tests.Integration;

[Collection("HostStartupCollection")]
public class ProductionStartupSecurityIntegrationTests
{
    private const string ValidSecureKey = "a_super_secure_production_signing_key_that_is_at_least_32_bytes_long_256bits!";
    private const string ShortKey = "short_key_under_32_bytes";
    private const string PlaceholderKey = "REPLACE_WITH_SECURE_SECRET_IN_ENVIRONMENT_OR_USER_SECRETS";

    private static readonly SemaphoreSlim _environmentLock = new(1, 1);

    private static void RunWithEnvironment(IDictionary<string, string?> envVars, Action action)
    {
        _environmentLock.Wait();
        try
        {
            var original = new Dictionary<string, string?>();
            foreach (var key in envVars.Keys)
            {
                original[key] = Environment.GetEnvironmentVariable(key);
            }

            try
            {
                foreach (var (key, value) in envVars)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }

                action();
            }
            finally
            {
                foreach (var (key, value) in original)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
        finally
        {
            _environmentLock.Release();
        }
    }

    private static async Task RunWithEnvironmentAsync(IDictionary<string, string?> envVars, Func<Task> action)
    {
        await _environmentLock.WaitAsync();
        try
        {
            var original = new Dictionary<string, string?>();
            foreach (var key in envVars.Keys)
            {
                original[key] = Environment.GetEnvironmentVariable(key);
            }

            try
            {
                foreach (var (key, value) in envVars)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }

                await action();
            }
            finally
            {
                foreach (var (key, value) in original)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
        finally
        {
            _environmentLock.Release();
        }
    }

    [Fact]
    public void ProductionStartup_WhenJwtSigningKeyIsMissing_FailsImmediatelyAtStartup()
    {
        RunWithEnvironment(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Jwt__Key"] = null,
            ["Jwt__SecretKey"] = null
        }, () =>
        {
            using var factory = new WebApplicationFactory<Program>();
            var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

            Assert.Contains("JWT signing key is missing", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret_val", ex.Message);
        });
    }

    [Fact]
    public void ProductionStartup_WhenJwtKeyIsPlaceholder_FailsImmediatelyAtStartup()
    {
        RunWithEnvironment(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Jwt__Key"] = PlaceholderKey,
            ["Jwt__SecretKey"] = null
        }, () =>
        {
            using var factory = new WebApplicationFactory<Program>();
            var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

            Assert.Contains("unsafe development placeholder", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(PlaceholderKey, ex.Message);
        });
    }

    [Fact]
    public void ProductionStartup_WhenJwtKeyIsTooShort_FailsImmediatelyAtStartup()
    {
        RunWithEnvironment(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Jwt__Key"] = ShortKey,
            ["Jwt__SecretKey"] = null
        }, () =>
        {
            using var factory = new WebApplicationFactory<Program>();
            var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

            Assert.Contains("too short", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("256 bits", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ShortKey, ex.Message);
        });
    }

    [Fact]
    public async Task ProductionStartup_WhenValidExternalKeyProvidedViaJwtKey_StartsSuccessfully()
    {
        await RunWithEnvironmentAsync(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Jwt__Key"] = ValidSecureKey,
            ["Jwt__SecretKey"] = null,
            ["ConnectionStrings__DefaultConnection"] = "Server=localhost;Database=SkillSwap_IntegrationTestDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"
        }, async () =>
        {
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            var response = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal("Healthy", body);
        });
    }

    [Fact]
    public async Task ProductionStartup_WhenValidExternalKeyProvidedViaJwtSecretKey_StartsSuccessfully()
    {
        await RunWithEnvironmentAsync(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Jwt__Key"] = null,
            ["Jwt__SecretKey"] = ValidSecureKey,
            ["ConnectionStrings__DefaultConnection"] = "Server=localhost;Database=SkillSwap_IntegrationTestDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"
        }, async () =>
        {
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            var response = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal("Healthy", body);
        });
    }

    [Fact]
    public async Task DevelopmentStartup_WithDevelopmentSettings_StartsSuccessfully()
    {
        await RunWithEnvironmentAsync(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["Jwt__Key"] = null,
            ["Jwt__SecretKey"] = null,
            ["ConnectionStrings__DefaultConnection"] = "Server=localhost;Database=SkillSwap_IntegrationTestDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"
        }, async () =>
        {
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            var response = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal("Healthy", body);
        });
    }

    // ── Swagger configuration-controlled behavior ───────────────────────────

    /// <summary>
    /// In Production with no Swagger:Enabled setting, Swagger must be hidden.
    /// Verifies that the default (absent key) correctly disables the middleware.
    /// </summary>
    [Fact]
    public async Task ProductionStartup_WithNoSwaggerEnabled_SwaggerEndpointReturns404()
    {
        await RunWithEnvironmentAsync(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Jwt__SecretKey"] = ValidSecureKey,
            ["Swagger__Enabled"] = null, // absent — must default to disabled
            ["ConnectionStrings__DefaultConnection"] = "Server=localhost;Database=SkillSwap_IntegrationTestDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"
        }, async () =>
        {
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            var response = await client.GetAsync("/swagger/index.html");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    /// <summary>
    /// In Production with Swagger:Enabled=false, Swagger must be hidden.
    /// Verifies that an explicit false does not accidentally enable the UI.
    /// </summary>
    [Fact]
    public async Task ProductionStartup_WithSwaggerEnabledFalse_SwaggerEndpointReturns404()
    {
        await RunWithEnvironmentAsync(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Jwt__SecretKey"] = ValidSecureKey,
            ["Swagger__Enabled"] = "false",
            ["ConnectionStrings__DefaultConnection"] = "Server=localhost;Database=SkillSwap_IntegrationTestDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"
        }, async () =>
        {
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            var response = await client.GetAsync("/swagger/index.html");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    /// <summary>
    /// In Production with Swagger:Enabled=true, Swagger UI must be accessible.
    /// Verifies that the opt-in configuration flag correctly enables the middleware.
    /// Authentication and authorization on API endpoints are unaffected.
    /// </summary>
    [Fact]
    public async Task ProductionStartup_WithSwaggerEnabledTrue_SwaggerEndpointReturns200()
    {
        await RunWithEnvironmentAsync(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Jwt__SecretKey"] = ValidSecureKey,
            ["Swagger__Enabled"] = "true",
            ["ConnectionStrings__DefaultConnection"] = "Server=localhost;Database=SkillSwap_IntegrationTestDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"
        }, async () =>
        {
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            var response = await client.GetAsync("/swagger/index.html");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });
    }

    /// <summary>
    /// In Development, Swagger is always enabled regardless of the Swagger:Enabled setting.
    /// Verifies that the IsDevelopment() path takes precedence.
    /// </summary>
    [Fact]
    public async Task DevelopmentStartup_SwaggerAlwaysEnabled_RegardlessOfSwaggerEnabledSetting()
    {
        await RunWithEnvironmentAsync(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["Swagger__Enabled"] = "false", // explicitly false — should be ignored in Development
            ["ConnectionStrings__DefaultConnection"] = "Server=localhost;Database=SkillSwap_IntegrationTestDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"
        }, async () =>
        {
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            var response = await client.GetAsync("/swagger/index.html");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });
    }
}
