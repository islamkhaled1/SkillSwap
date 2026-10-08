using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SkillSwap.Domain.Entities;

namespace SkillSwap.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<UserProfile> UserProfiles { get; }
    DbSet<Category> Categories { get; }
    DbSet<Skill> Skills { get; }
    DbSet<UserSkill> UserSkills { get; }
    DbSet<AvailabilitySlot> AvailabilitySlots { get; }
    DbSet<SwapRequest> SwapRequests { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<ConversationParticipant> ConversationParticipants { get; }
    DbSet<Message> Messages { get; }
    DbSet<Session> Sessions { get; }
    DbSet<Wallet> Wallets { get; }
    DbSet<CreditTransaction> CreditTransactions { get; }
    DbSet<Review> Reviews { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<UserSubscription> UserSubscriptions { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Badge> Badges { get; }
    DbSet<UserBadge> UserBadges { get; }
    DbSet<Favorite> Favorites { get; }
    DbSet<UserBlock> UserBlocks { get; }
    DbSet<Report> Reports { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
