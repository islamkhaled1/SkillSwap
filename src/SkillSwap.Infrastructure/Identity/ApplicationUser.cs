using Microsoft.AspNetCore.Identity;

namespace SkillSwap.Infrastructure.Identity;

/// <summary>
/// Custom Identity user extending IdentityUser with a Guid key.
/// Future fields (profile, preferences, etc.) will be added through
/// UserProfile and other related entities, keeping this class lean.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>UTC timestamp when the account was created.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last profile update.</summary>
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>
    /// Soft-delete flag. Deleted accounts are not physically removed
    /// to preserve session, wallet, and ledger history integrity.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the account was soft-deleted.</summary>
    public DateTime? DeletedAtUtc { get; set; }
}
