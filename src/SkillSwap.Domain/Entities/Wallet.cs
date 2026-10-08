namespace SkillSwap.Domain.Entities;

public class Wallet
{
    public Guid UserId { get; set; }
    public int AvailableMinutes { get; set; }
    public int HeldMinutes { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];

    public ICollection<CreditTransaction> CreditTransactions { get; set; } = new List<CreditTransaction>();
}
