using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Identity;

namespace SkillSwap.Infrastructure.Persistence.Configurations;

public class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.ToTable("Favorites", t =>
        {
            t.HasCheckConstraint("CK_Favorite_User_NotSelf", "[UserId] <> [FavoriteUserId]");
        });

        builder.HasKey(f => new { f.UserId, f.FavoriteUserId });

        builder.Property(f => f.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(f => f.FavoriteUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
