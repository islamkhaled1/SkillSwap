using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Identity;

namespace SkillSwap.Infrastructure.Persistence.Configurations;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallets", t =>
        {
            t.HasCheckConstraint("CK_Wallet_AvailableMinutes", "[AvailableMinutes] >= 0");
            t.HasCheckConstraint("CK_Wallet_HeldMinutes", "[HeldMinutes] >= 0");
        });

        builder.HasKey(w => w.UserId);

        builder.Property(w => w.AvailableMinutes)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(w => w.HeldMinutes)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(w => w.UpdatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(w => w.RowVersion)
            .IsRowVersion();

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<Wallet>(w => w.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
