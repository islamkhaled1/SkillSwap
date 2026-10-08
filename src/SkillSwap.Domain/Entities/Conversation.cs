namespace SkillSwap.Domain.Entities;

public class Conversation
{
    public long Id { get; set; }
    public long SwapRequestId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public SwapRequest SwapRequest { get; set; } = null!;
    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
