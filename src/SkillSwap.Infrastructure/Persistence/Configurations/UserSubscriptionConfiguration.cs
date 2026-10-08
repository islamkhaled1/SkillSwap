using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Identity;

namespace SkillSwap.Infrastructure.Persistence.Configurations;

public class UserSubscriptionConfiguration : IEntityTypeConfiguration<UserSubscription>
{
    public void Configure(EntityTypeBuilder<UserSubscription> builder)
    {
        builder.ToTable("UserSubscriptions");

        builder.HasKey(us => us.Id);
        builder.Property(us => us.Id)
            .ValueGeneratedOnAdd();

        builder.Property(us => us.StartsAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(us => us.EndsAtUtc)
            .HasColumnType("datetime2");

        builder.Property(us => us.Status)
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(us => us.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasIndex(us => us.UserId)
            .IsUnique()
            .HasFilter("[Status] = 1")
            .HasDatabaseName("UX_UserSubscription_ActiveUser");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(us => us.Plan)
            .WithMany(p => p.UserSubscriptions)
            .HasForeignKey(us => us.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
