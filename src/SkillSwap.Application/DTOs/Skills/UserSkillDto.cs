using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.DTOs.Skills;

public sealed class UserSkillDto
{
    public long Id { get; init; }
    public Guid UserId { get; init; }
    public int SkillId { get; init; }
    public string SkillName { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public UserSkillType Type { get; init; }
    public UserSkillLevel Level { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
