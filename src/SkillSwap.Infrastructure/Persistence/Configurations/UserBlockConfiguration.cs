using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Identity;

namespace SkillSwap.Infrastructure.Persistence.Configurations;

public class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> builder)
    {
        builder.ToTable("UserBlocks", t =>
        {
            t.HasCheckConstraint("CK_UserBlock_Blocker_NotSelf", "[BlockerId] <> [BlockedId]");
        });

        builder.HasKey(ub => new { ub.BlockerId, ub.BlockedId });

        builder.Property(ub => ub.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(ub => ub.BlockerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(ub => ub.BlockedId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
