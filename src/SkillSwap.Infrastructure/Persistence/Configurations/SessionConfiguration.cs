using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Identity;

namespace SkillSwap.Infrastructure.Persistence.Configurations;

/// <summary>
/// Session configuration.
///
/// NOTE ON OVERLAP PREVENTION:
/// SQL Server does not support range exclusion constraints like PostgreSQL.
/// Session overlap prevention is enforced in the service layer using a database transaction
/// with UPDLOCK on learner and teacher Wallet rows in deterministic order.
/// </summary>
public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("Sessions", t =>
        {
            t.HasCheckConstraint("CK_Session_Teacher_Learner", "[TeacherId] <> [LearnerId]");
            t.HasCheckConstraint("CK_Session_DurationMinutes", "[DurationMinutes] IN (30, 60, 90)");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .ValueGeneratedOnAdd();

        builder.Property(s => s.StartUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(s => s.EndUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(s => s.DurationMinutes)
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(s => s.Mode)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(s => s.MeetingUrl)
            .HasMaxLength(1000);

        builder.Property(s => s.Status)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(s => s.CancelledAtUtc)
            .HasColumnType("datetime2");

        builder.Property(s => s.LearnerJoinedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(s => s.TeacherJoinedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(s => s.ResolvedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(s => s.ResolutionNote)
            .HasMaxLength(1000);

        builder.Property(s => s.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(s => s.UpdatedAtUtc)
            .HasColumnType("datetime2");

        builder.Property(s => s.RowVersion)
            .IsRowVersion();

        builder.HasIndex(s => new { s.TeacherId, s.StartUtc })
            .HasDatabaseName("IX_Session_TeacherId_StartUtc");

        builder.HasIndex(s => new { s.LearnerId, s.StartUtc })
            .HasDatabaseName("IX_Session_LearnerId_StartUtc");

        builder.HasIndex(s => new { s.Status, s.EndUtc })
            .HasDatabaseName("IX_Session_Status_EndUtc");

        builder.HasOne(s => s.SwapRequest)
            .WithMany(sr => sr.Sessions)
            .HasForeignKey(s => s.SwapRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.LearnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Skill)
            .WithMany(sk => sk.Sessions)
            .HasForeignKey(s => s.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.CancelledById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.NoShowUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.ReportedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
