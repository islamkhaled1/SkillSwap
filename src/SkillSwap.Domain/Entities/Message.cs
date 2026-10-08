namespace SkillSwap.Domain.Entities;

public class Message
{
    public long Id { get; set; }
    public long ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public Conversation Conversation { get; set; } = null!;
}
