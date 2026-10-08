using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillSwap.Domain.Entities;

namespace SkillSwap.Infrastructure.Persistence.Configurations;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("SubscriptionPlans");

        builder.HasKey(sp => sp.Id);
        builder.Property(sp => sp.Id)
            .ValueGeneratedOnAdd();

        builder.Property(sp => sp.Code)
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.HasIndex(sp => sp.Code)
            .IsUnique();

        builder.Property(sp => sp.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(sp => sp.MonthlyLearningMinutes)
            .IsRequired();

        builder.Property(sp => sp.Price)
            .HasColumnType("decimal(10,2)")
            .IsRequired(false);

        builder.Property(sp => sp.PriorityMatching)
            .IsRequired();

        builder.Property(sp => sp.AdvancedSearch)
            .IsRequired();

        builder.Property(sp => sp.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Seed initial static subscription plans.
        // Price for PREMIUM is null because monetization/pricing is intentionally postponed in the MVP.
        builder.HasData(
            new SubscriptionPlan
            {
                Id = 1,
                Code = "FREE",
                Name = "Free",
                MonthlyLearningMinutes = 180,
                Price = 0.00m,
                PriorityMatching = false,
                AdvancedSearch = false,
                IsActive = true
            },
            new SubscriptionPlan
            {
                Id = 2,
                Code = "PREMIUM",
                Name = "Premium",
                MonthlyLearningMinutes = 720,
                Price = null,
                PriorityMatching = true,
                AdvancedSearch = true,
                IsActive = true
            }
        );
    }
}
