namespace SkillSwap.Application.DTOs.Blocks;

public sealed class BlockedUserDto
{
    public Guid BlockedId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
