namespace SkillSwap.Domain.Entities;

public class Skill
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Category Category { get; set; } = null!;
    public ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
    public ICollection<SwapRequest> SwapRequests { get; set; } = new List<SwapRequest>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}
