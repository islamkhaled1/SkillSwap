using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillSwap.Domain.Entities;

namespace SkillSwap.Infrastructure.Persistence.Configurations;

public class CreditTransactionConfiguration : IEntityTypeConfiguration<CreditTransaction>
{
    public void Configure(EntityTypeBuilder<CreditTransaction> builder)
    {
        builder.ToTable("CreditTransactions");

        builder.HasKey(ct => ct.Id);
        builder.Property(ct => ct.Id)
            .ValueGeneratedOnAdd();

        builder.Property(ct => ct.Type)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(ct => ct.AvailableDelta)
            .IsRequired();

        builder.Property(ct => ct.HeldDelta)
            .IsRequired();

        builder.Property(ct => ct.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(ct => ct.Description)
            .HasMaxLength(500);

        // Filtered unique index to protect against duplicate Hold/Release/Capture/Earn operations for the same Session + Wallet
        builder.HasIndex(ct => new { ct.WalletId, ct.SessionId, ct.Type })
            .IsUnique()
            .HasFilter("[SessionId] IS NOT NULL AND [Type] IN (2, 3, 4, 5)")
            .HasDatabaseName("UX_CreditTransaction_Wallet_Session_Type");

        builder.HasIndex(ct => new { ct.WalletId, ct.CreatedAtUtc })
            .HasDatabaseName("IX_CreditTransaction_WalletId_CreatedAtUtc");

        builder.HasIndex(ct => ct.SessionId)
            .HasDatabaseName("IX_CreditTransaction_SessionId");

        builder.HasOne(ct => ct.Wallet)
            .WithMany(w => w.CreditTransactions)
            .HasForeignKey(ct => ct.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ct => ct.Session)
            .WithMany(s => s.CreditTransactions)
            .HasForeignKey(ct => ct.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
