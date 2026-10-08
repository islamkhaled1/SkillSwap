namespace SkillSwap.Domain.Common;

/// <summary>
/// Base class for all domain entities with a strongly-typed Id.
/// </summary>
public abstract class BaseEntity<TId>
{
    public TId Id { get; protected set; } = default!;
    public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; protected set; }

    protected void SetUpdatedAt() => UpdatedAtUtc = DateTime.UtcNow;
}

/// <summary>
/// Convenience base with Guid Id (the most common case).
/// </summary>
public abstract class BaseEntity : BaseEntity<Guid>
{
    protected BaseEntity()
    {
        Id = Guid.NewGuid();
    }
}
