using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Identity;

namespace SkillSwap.Infrastructure.Persistence.Configurations;

public class AvailabilitySlotConfiguration : IEntityTypeConfiguration<AvailabilitySlot>
{
    public void Configure(EntityTypeBuilder<AvailabilitySlot> builder)
    {
        builder.ToTable("AvailabilitySlots", t =>
        {
            t.HasCheckConstraint("CK_AvailabilitySlot_StartTime_EndTime", "[StartTime] < [EndTime]");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd();

        builder.Property(a => a.DayOfWeek)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(a => a.StartTime)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(a => a.EndTime)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(a => a.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasIndex(a => new { a.UserId, a.DayOfWeek });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
