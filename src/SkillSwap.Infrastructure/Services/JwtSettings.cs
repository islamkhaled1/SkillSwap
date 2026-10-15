namespace SkillSwap.Infrastructure.Services;

/// <summary>
/// Strongly-typed configuration for JWT token generation.
/// Values are loaded from appsettings.json -> "Jwt" section.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    private string _secretKey = string.Empty;

    /// <summary>JWT signing secret. Supported via SecretKey or Key configuration. In production, this must be provided via external configuration (e.g. Jwt__Key or Jwt__SecretKey).</summary>
    public string SecretKey
    {
        get => !string.IsNullOrWhiteSpace(_secretKey) ? _secretKey : Key;
        set => _secretKey = value;
    }

    /// <summary>Alternative property mapping for Jwt:Key / Jwt__Key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Resolved signing key to use for symmetric signing and token validation.</summary>
    public string SigningKey => SecretKey;

    /// <summary>Token issuer (e.g. "SkillSwap").</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Token audience (e.g. "SkillSwap.Client").</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Token lifetime in minutes. Default: 60.</summary>
    public int ExpiryMinutes { get; set; } = 60;
}
