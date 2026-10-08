using SkillSwap.Domain.Enums;

namespace SkillSwap.Domain.Entities;

public class UserSkill
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public int SkillId { get; set; }
    public UserSkillType Type { get; set; }
    public UserSkillLevel Level { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Skill Skill { get; set; } = null!;
}
