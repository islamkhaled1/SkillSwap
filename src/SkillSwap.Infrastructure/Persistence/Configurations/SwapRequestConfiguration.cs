using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Identity;

namespace SkillSwap.Infrastructure.Persistence.Configurations;

public class SwapRequestConfiguration : IEntityTypeConfiguration<SwapRequest>
{
    public void Configure(EntityTypeBuilder<SwapRequest> builder)
    {
        builder.ToTable("SwapRequests", t =>
        {
            t.HasCheckConstraint("CK_SwapRequest_Requester_Receiver", "[RequesterId] <> [ReceiverId]");
        });

        builder.HasKey(sr => sr.Id);
        builder.Property(sr => sr.Id)
            .ValueGeneratedOnAdd();

        builder.Property(sr => sr.Message)
            .HasMaxLength(1000);

        builder.Property(sr => sr.Status)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(sr => sr.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(sr => sr.RespondedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(sr => sr.ExpiresAtUtc)
            .HasColumnType("datetime2");

        builder.Property(sr => sr.CancelledAtUtc)
            .HasColumnType("datetime2");

        builder.HasIndex(sr => new { sr.RequesterId, sr.ReceiverId, sr.SkillId })
            .IsUnique()
            .HasFilter("[Status] = 1")
            .HasDatabaseName("UX_SwapRequest_ActivePending");

        builder.HasIndex(sr => new { sr.RequesterId, sr.Status });
        builder.HasIndex(sr => new { sr.ReceiverId, sr.Status });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(sr => sr.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(sr => sr.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sr => sr.Skill)
            .WithMany(s => s.SwapRequests)
            .HasForeignKey(sr => sr.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
