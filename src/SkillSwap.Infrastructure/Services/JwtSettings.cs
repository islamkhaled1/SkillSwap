namespace SkillSwap.Infrastructure.Services;

/// <summary>
/// Strongly-typed configuration for JWT token generation.
/// Values are loaded from appsettings.json -> "Jwt" section.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>JWT signing secret. MUST be provided via environment variable in production.</summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Token issuer (e.g. "SkillSwap").</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Token audience (e.g. "SkillSwap.Client").</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>Token lifetime in minutes. Default: 60.</summary>
    public int ExpiryMinutes { get; init; } = 60;
}
