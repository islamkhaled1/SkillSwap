using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillSwap.Domain.Entities;

namespace SkillSwap.Infrastructure.Persistence.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .ValueGeneratedOnAdd();

        builder.Property(c => c.CreatedAtUtc)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasIndex(c => c.SwapRequestId)
            .IsUnique()
            .HasDatabaseName("UX_Conversation_SwapRequestId");

        builder.HasOne(c => c.SwapRequest)
            .WithOne(sr => sr.Conversation)
            .HasForeignKey<Conversation>(c => c.SwapRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
