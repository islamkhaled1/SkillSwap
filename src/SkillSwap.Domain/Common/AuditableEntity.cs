namespace SkillSwap.Domain.Common;

/// <summary>
/// Extends BaseEntity with soft-delete and audit tracking.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public bool IsDeleted { get; protected set; }
    public DateTime? DeletedAtUtc { get; protected set; }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        SetUpdatedAt();
    }
}
