using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Identity;

namespace SkillSwap.Infrastructure.Persistence.Configurations;

public class UserSkillConfiguration : IEntityTypeConfiguration<UserSkill>
{
    public void Configure(EntityTypeBuilder<UserSkill> builder)
    {
        builder.ToTable("UserSkills");

        builder.HasKey(us => us.Id);
        builder.Property(us => us.Id)
            .ValueGeneratedOnAdd();

        builder.Property(us => us.Type)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(us => us.Level)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(us => us.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasIndex(us => new { us.UserId, us.SkillId, us.Type })
            .IsUnique();

        builder.HasIndex(us => new { us.SkillId, us.Type, us.UserId })
            .HasDatabaseName("IX_UserSkill_SkillId_Type_UserId");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(us => us.Skill)
            .WithMany(s => s.UserSkills)
            .HasForeignKey(us => us.SkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
