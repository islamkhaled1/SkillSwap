using SkillSwap.Domain.Enums;

namespace SkillSwap.Domain.Entities;

public class SwapRequest
{
    public long Id { get; set; }
    public Guid RequesterId { get; set; }
    public Guid ReceiverId { get; set; }
    public int SkillId { get; set; }
    public string? Message { get; set; }
    public SwapRequestStatus Status { get; set; } = SwapRequestStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }

    public Skill Skill { get; set; } = null!;
    public Conversation? Conversation { get; set; }
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}
