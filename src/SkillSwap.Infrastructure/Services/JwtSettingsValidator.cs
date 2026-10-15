using System.Text;

namespace SkillSwap.Infrastructure.Services;

/// <summary>
/// Validates JWT configuration settings, enforcing production security invariants,
/// minimum key lengths, and placeholder rejection without leaking sensitive values.
/// </summary>
public static class JwtSettingsValidator
{
    public const int MinimumKeySizeBytes = 32; // 256 bits required for HMAC-SHA256

    private static readonly string[] PlaceholderSubstrings =
    [
        "REPLACE_WITH",
        "PLACEHOLDER",
        "DEVELOPMENT_ONLY",
        "DEV_ONLY",
        "DO_NOT_USE",
        "CHANGEME",
        "CHANGE_ME",
        "INSECURE",
        "DEFAULT_SECRET",
        "YOUR_SECRET",
        "MY_SECRET"
    ];

    private static readonly HashSet<string> KnownWeakSecrets = new(StringComparer.OrdinalIgnoreCase)
    {
        "secret",
        "secretkey",
        "password",
        "12345678901234567890123456789012",
        "abcdefghijklmnopqrstuvwxyz123456"
    };

    /// <summary>
    /// Validates the JWT settings according to the target environment.
    /// In Production, the signing key must be non-empty, at least 256 bits, and not an unsafe placeholder.
    /// In non-Production environments, the signing key must be non-empty and at least 256 bits.
    /// Never includes the secret value in exception messages.
    /// </summary>
    public static void Validate(JwtSettings settings, bool isProduction)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var signingKey = settings.SigningKey;

        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException(
                "JWT signing key is missing. In Production, provide a cryptographically secure key via the " +
                "'Jwt__Key' (or 'Jwt:SecretKey') environment variable or a supported secret provider.");
        }

        var byteCount = Encoding.UTF8.GetByteCount(signingKey);
        if (byteCount < MinimumKeySizeBytes)
        {
            throw new InvalidOperationException(
                $"JWT signing key is too short ({byteCount * 8} bits). The key must be at least 256 bits (32 bytes) long for HMAC-SHA256.");
        }

        if (isProduction && IsUnsafePlaceholder(signingKey))
        {
            throw new InvalidOperationException(
                "An unsafe development placeholder was detected for the JWT signing key in Production. " +
                "In Production, a cryptographically secure key must be provided via the 'Jwt__Key' " +
                "environment variable or a supported secret provider.");
        }
    }

    /// <summary>
    /// Checks whether the supplied signing key is an unsafe development placeholder or weak default.
    /// </summary>
    public static bool IsUnsafePlaceholder(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return true;

        if (KnownWeakSecrets.Contains(key))
            return true;

        foreach (var placeholder in PlaceholderSubstrings)
        {
            if (key.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
