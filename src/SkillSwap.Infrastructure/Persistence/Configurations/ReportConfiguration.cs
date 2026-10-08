using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Identity;

namespace SkillSwap.Infrastructure.Persistence.Configurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Reports", t =>
        {
            t.HasCheckConstraint("CK_Report_Reporter_NotReported", "[ReporterId] <> [ReportedUserId]");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .ValueGeneratedOnAdd();

        builder.Property(r => r.Reason)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(r => r.Description)
            .HasMaxLength(1000);

        builder.Property(r => r.Status)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(r => r.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(r => r.ResolvedAtUtc)
            .HasColumnType("datetime2");

        builder.HasIndex(r => new { r.ReportedUserId, r.Status })
            .HasDatabaseName("IX_Report_ReportedUserId_Status");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.ReportedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Session)
            .WithMany(s => s.Reports)
            .HasForeignKey(r => r.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
