using SkillSwap.Domain.Enums;

namespace SkillSwap.Domain.Entities;

public class CreditTransaction
{
    public long Id { get; set; }
    public Guid WalletId { get; set; }
    public long? SessionId { get; set; }
    public CreditTransactionType Type { get; set; }
    public int AvailableDelta { get; set; }
    public int HeldDelta { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }

    public Wallet Wallet { get; set; } = null!;
    public Session? Session { get; set; }
}
