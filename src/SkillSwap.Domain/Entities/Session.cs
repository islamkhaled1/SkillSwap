using SkillSwap.Domain.Enums;

namespace SkillSwap.Domain.Entities;

public class Session
{
    public long Id { get; set; }
    public long SwapRequestId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid LearnerId { get; set; }
    public int SkillId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public short DurationMinutes { get; set; }
    public SessionMode Mode { get; set; } = SessionMode.Online;
    public string? MeetingUrl { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

    public Guid? CancelledById { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public Guid? NoShowUserId { get; set; }
    public DateTime? LearnerJoinedAtUtc { get; set; }
    public DateTime? TeacherJoinedAtUtc { get; set; }

    // Dispute metadata
    public Guid? ReportedById { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNote { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public SwapRequest SwapRequest { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
    public ICollection<CreditTransaction> CreditTransactions { get; set; } = new List<CreditTransaction>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Report> Reports { get; set; } = new List<Report>();
}
