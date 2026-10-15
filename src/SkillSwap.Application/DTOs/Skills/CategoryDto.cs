namespace SkillSwap.Application.DTOs.Skills;

public sealed class CategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int ActiveSkillsCount { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
