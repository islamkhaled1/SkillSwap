namespace SkillSwap.Domain.Entities;

public class ConversationParticipant
{
    public long ConversationId { get; set; }
    public Guid UserId { get; set; }
    public long? LastReadMessageId { get; set; }
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;

    public Conversation Conversation { get; set; } = null!;
}
