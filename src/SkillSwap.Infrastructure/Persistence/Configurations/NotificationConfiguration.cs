using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Identity;

namespace SkillSwap.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .ValueGeneratedOnAdd();

        builder.Property(n => n.Type)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(n => n.Payload)
            .IsRequired();

        builder.Property(n => n.IsRead)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.ReadAtUtc)
            .HasColumnType("datetime2");

        builder.Property(n => n.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAtUtc })
            .HasDatabaseName("IX_Notification_UserId_IsRead_CreatedAtUtc");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
