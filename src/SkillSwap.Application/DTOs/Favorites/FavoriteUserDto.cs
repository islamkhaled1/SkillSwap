namespace SkillSwap.Application.DTOs.Favorites;

public sealed class FavoriteUserDto
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public string? Bio { get; init; }
    public DateTime AddedAtUtc { get; init; }
}
