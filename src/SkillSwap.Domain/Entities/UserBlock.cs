namespace SkillSwap.Domain.Entities;

public class UserBlock
{
    public Guid BlockerId { get; set; }
    public Guid BlockedId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
