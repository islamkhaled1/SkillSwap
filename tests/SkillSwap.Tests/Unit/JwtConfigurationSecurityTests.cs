using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SkillSwap.API.Extensions;
using SkillSwap.Infrastructure.Services;

namespace SkillSwap.Tests.Unit;

public class JwtConfigurationSecurityTests
{
    private const string ValidSecureKey = "a_super_secure_production_signing_key_that_is_at_least_32_bytes_long_256bits!";
    private const string ShortKey = "short_key_under_32_bytes";
    private const string PlaceholderKey = "REPLACE_WITH_SECURE_SECRET_IN_ENVIRONMENT_OR_USER_SECRETS";
    private const string DevKey = "DEVELOPMENT_ONLY_INSECURE_TEST_SIGNING_KEY_FOR_LOCAL_DEV_ENVIRONMENT_DO_NOT_USE_IN_PRODUCTION";

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "SkillSwap.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static IHostEnvironment CreateMockEnvironment(string environmentName) =>
        new TestHostEnvironment { EnvironmentName = environmentName };

    [Fact]
    public void Production_WhenSigningKeyIsMissing_ThrowsInvalidOperationException_WithoutExposingSecret()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "SkillSwap",
                ["Jwt:Audience"] = "SkillSwap.Client",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

        var services = new ServiceCollection();
        var prodEnv = CreateMockEnvironment(Environments.Production);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSkillSwapAuthentication(configuration, prodEnv));

        Assert.Contains("JWT signing key is missing", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_WhenSigningKeyIsEmptyString_ThrowsInvalidOperationException()
    {
        // Arrange (simulating empty string in appsettings.json)
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "",
                ["Jwt:Issuer"] = "SkillSwap",
                ["Jwt:Audience"] = "SkillSwap.Client"
            })
            .Build();

        var services = new ServiceCollection();
        var prodEnv = CreateMockEnvironment(Environments.Production);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSkillSwapAuthentication(configuration, prodEnv));

        Assert.Contains("JWT signing key is missing", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_WhenSigningKeyIsTooShort_ThrowsInvalidOperationException()
    {
        // Arrange (key is under 32 bytes / 256 bits)
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = ShortKey,
                ["Jwt:Issuer"] = "SkillSwap",
                ["Jwt:Audience"] = "SkillSwap.Client"
            })
            .Build();

        var services = new ServiceCollection();
        var prodEnv = CreateMockEnvironment(Environments.Production);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSkillSwapAuthentication(configuration, prodEnv));

        Assert.Contains("too short", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("256 bits", ex.Message, StringComparison.OrdinalIgnoreCase);
        // Verify secret value is never exposed in the error message
        Assert.DoesNotContain(ShortKey, ex.Message);
    }

    [Fact]
    public void Production_WhenSigningKeyIsPlaceholder_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = PlaceholderKey,
                ["Jwt:Issuer"] = "SkillSwap",
                ["Jwt:Audience"] = "SkillSwap.Client"
            })
            .Build();

        var services = new ServiceCollection();
        var prodEnv = CreateMockEnvironment(Environments.Production);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSkillSwapAuthentication(configuration, prodEnv));

        Assert.Contains("unsafe development placeholder", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PlaceholderKey, ex.Message);
    }

    [Fact]
    public void Production_WhenSigningKeyIsDevelopmentKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = DevKey,
                ["Jwt:Issuer"] = "SkillSwap",
                ["Jwt:Audience"] = "SkillSwap.Client"
            })
            .Build();

        var services = new ServiceCollection();
        var prodEnv = CreateMockEnvironment(Environments.Production);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSkillSwapAuthentication(configuration, prodEnv));

        Assert.Contains("unsafe development placeholder", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(DevKey, ex.Message);
    }

    [Fact]
    public void Production_WhenValidKeyProvidedViaJwtKey_Succeeds()
    {
        // Arrange (simulating environment variable Jwt__Key -> configuration["Jwt:Key"])
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = ValidSecureKey,
                ["Jwt:Issuer"] = "SkillSwap",
                ["Jwt:Audience"] = "SkillSwap.Client"
            })
            .Build();

        var services = new ServiceCollection();
        var prodEnv = CreateMockEnvironment(Environments.Production);

        // Act & Assert: Should not throw
        services.AddSkillSwapAuthentication(configuration, prodEnv);
    }

    [Fact]
    public void Production_WhenValidKeyProvidedViaJwtSecretKey_Succeeds()
    {
        // Arrange (simulating environment variable Jwt__SecretKey -> configuration["Jwt:SecretKey"])
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = ValidSecureKey,
                ["Jwt:Issuer"] = "SkillSwap",
                ["Jwt:Audience"] = "SkillSwap.Client"
            })
            .Build();

        var services = new ServiceCollection();
        var prodEnv = CreateMockEnvironment(Environments.Production);

        // Act & Assert: Should not throw
        services.AddSkillSwapAuthentication(configuration, prodEnv);
    }

    [Fact]
    public void Development_WhenUsingExplicitDevelopmentPlaceholder_Succeeds()
    {
        // Arrange (simulating appsettings.Development.json in Development environment)
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = DevKey,
                ["Jwt:Issuer"] = "SkillSwap",
                ["Jwt:Audience"] = "SkillSwap.Client"
            })
            .Build();

        var services = new ServiceCollection();
        var devEnv = CreateMockEnvironment(Environments.Development);

        // Act & Assert: Should not throw in Development
        services.AddSkillSwapAuthentication(configuration, devEnv);
    }

    [Fact]
    public void Development_WhenKeyIsTooShort_ThrowsInvalidOperationException()
    {
        // Arrange (even in development, HS256 requires 256 bits)
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "too_short",
                ["Jwt:Issuer"] = "SkillSwap",
                ["Jwt:Audience"] = "SkillSwap.Client"
            })
            .Build();

        var services = new ServiceCollection();
        var devEnv = CreateMockEnvironment(Environments.Development);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSkillSwapAuthentication(configuration, devEnv));

        Assert.Contains("too short", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("too_short", ex.Message);
    }

    [Fact]
    public void JwtSettings_ResolvesKeyAndSecretKeyConsistently()
    {
        // When SecretKey is set directly
        var settings1 = new JwtSettings { SecretKey = "key_via_secret_key" };
        Assert.Equal("key_via_secret_key", settings1.SecretKey);
        Assert.Equal("key_via_secret_key", settings1.SigningKey);

        // When Key is set directly (e.g. from Jwt__Key)
        var settings2 = new JwtSettings { Key = "key_via_key_property" };
        Assert.Equal("key_via_key_property", settings2.SecretKey);
        Assert.Equal("key_via_key_property", settings2.SigningKey);
    }

    [Fact]
    public async Task JwtTokenService_GeneratesValidToken_WithConsistentIssuerAudienceAndKey()
    {
        // Arrange
        var settings = new JwtSettings
        {
            Key = ValidSecureKey,
            Issuer = "SkillSwap.TestIssuer",
            Audience = "SkillSwap.TestAudience",
            ExpiryMinutes = 30
        };

        var options = Options.Create(settings);
        var tokenService = new JwtTokenService(options);
        var userId = Guid.NewGuid();
        var email = "user@test.com";
        var roles = new[] { "User", "Admin" };

        // Act
        var tokenString = await tokenService.GenerateTokenAsync(userId, email, roles);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(tokenString));

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.ReadJwtToken(tokenString);

        Assert.Equal("SkillSwap.TestIssuer", token.Issuer);
        Assert.Contains("SkillSwap.TestAudience", token.Audiences);

        // Validate using symmetric key
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "SkillSwap.TestIssuer",
            ValidateAudience = true,
            ValidAudience = "SkillSwap.TestAudience",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ValidSecureKey)),
            ValidateLifetime = true
        };

        var principal = tokenHandler.ValidateToken(tokenString, validationParameters, out var validatedToken);
        Assert.NotNull(validatedToken);
        Assert.Equal(userId.ToString(), token.Subject);
        Assert.Equal(userId.ToString(), principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
    }
}
