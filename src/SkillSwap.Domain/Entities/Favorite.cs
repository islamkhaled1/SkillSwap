namespace SkillSwap.Domain.Entities;

public class Favorite
{
    public Guid UserId { get; set; }
    public Guid FavoriteUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
