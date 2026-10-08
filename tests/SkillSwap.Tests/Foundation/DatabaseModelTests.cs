using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Identity;
using SkillSwap.Infrastructure.Persistence;

namespace SkillSwap.Tests.Foundation;

/// <summary>
/// Verifies the complete EF Core database model, schema constraints,
/// indexes, and seed data configurations without connecting to a live database.
/// </summary>
public class DatabaseModelTests
{
    private static ApplicationDbContext CreateTestContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=TestDb;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public void Model_CanBeCreated_And_AllEntitiesRegistered()
    {
        using var context = CreateTestContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        Assert.NotNull(model.FindEntityType(typeof(ApplicationUser)));
        Assert.NotNull(model.FindEntityType(typeof(UserProfile)));
        Assert.NotNull(model.FindEntityType(typeof(Category)));
        Assert.NotNull(model.FindEntityType(typeof(Skill)));
        Assert.NotNull(model.FindEntityType(typeof(UserSkill)));
        Assert.NotNull(model.FindEntityType(typeof(AvailabilitySlot)));
        Assert.NotNull(model.FindEntityType(typeof(SwapRequest)));
        Assert.NotNull(model.FindEntityType(typeof(Conversation)));
        Assert.NotNull(model.FindEntityType(typeof(ConversationParticipant)));
        Assert.NotNull(model.FindEntityType(typeof(Message)));
        Assert.NotNull(model.FindEntityType(typeof(Session)));
        Assert.NotNull(model.FindEntityType(typeof(Wallet)));
        Assert.NotNull(model.FindEntityType(typeof(CreditTransaction)));
        Assert.NotNull(model.FindEntityType(typeof(Review)));
        Assert.NotNull(model.FindEntityType(typeof(SubscriptionPlan)));
        Assert.NotNull(model.FindEntityType(typeof(UserSubscription)));
        Assert.NotNull(model.FindEntityType(typeof(Notification)));
        Assert.NotNull(model.FindEntityType(typeof(Badge)));
        Assert.NotNull(model.FindEntityType(typeof(UserBadge)));
        Assert.NotNull(model.FindEntityType(typeof(Favorite)));
        Assert.NotNull(model.FindEntityType(typeof(UserBlock)));
        Assert.NotNull(model.FindEntityType(typeof(Report)));
    }

    [Fact]
    public void Model_Wallet_HasCheckConstraints_And_RowVersion()
    {
        using var context = CreateTestContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(Wallet));

        Assert.NotNull(entity);
        var checkConstraints = entity.GetCheckConstraints().Select(c => c.Name).ToList();
        Assert.Contains("CK_Wallet_AvailableMinutes", checkConstraints);
        Assert.Contains("CK_Wallet_HeldMinutes", checkConstraints);

        var rowVersion = entity.FindProperty(nameof(Wallet.RowVersion));
        Assert.NotNull(rowVersion);
        Assert.True(rowVersion.IsConcurrencyToken);
    }

    [Fact]
    public void Model_Session_HasCheckConstraints_And_ExpectedIndexes()
    {
        using var context = CreateTestContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(Session));

        Assert.NotNull(entity);
        var checkConstraints = entity.GetCheckConstraints().Select(c => c.Name).ToList();
        Assert.Contains("CK_Session_Teacher_Learner", checkConstraints);
        Assert.Contains("CK_Session_DurationMinutes", checkConstraints);

        var indexNames = entity.GetIndexes().Select(i => i.GetDatabaseName()).ToList();
        Assert.Contains("IX_Session_TeacherId_StartUtc", indexNames);
        Assert.Contains("IX_Session_LearnerId_StartUtc", indexNames);
        Assert.Contains("IX_Session_Status_EndUtc", indexNames);
    }

    [Fact]
    public void Model_FilteredIndexes_AreProperlyConfigured()
    {
        using var context = CreateTestContext();
        var model = context.GetService<IDesignTimeModel>().Model;

        // SwapRequest pending filtered unique index
        var swapRequestEntity = model.FindEntityType(typeof(SwapRequest));
        Assert.NotNull(swapRequestEntity);
        var pendingIndex = swapRequestEntity.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "UX_SwapRequest_ActivePending");
        Assert.NotNull(pendingIndex);
        Assert.True(pendingIndex.IsUnique);
        Assert.NotNull(pendingIndex.GetFilter());

        // UserSubscription active filtered unique index
        var userSubEntity = model.FindEntityType(typeof(UserSubscription));
        Assert.NotNull(userSubEntity);
        var activeSubIndex = userSubEntity.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "UX_UserSubscription_ActiveUser");
        Assert.NotNull(activeSubIndex);
        Assert.True(activeSubIndex.IsUnique);
        Assert.NotNull(activeSubIndex.GetFilter());
    }

    [Fact]
    public void Model_SubscriptionPlan_HasInitialSeedData()
    {
        using var context = CreateTestContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(SubscriptionPlan));

        Assert.NotNull(entity);
        var seedData = entity.GetSeedData().ToList();
        Assert.Equal(2, seedData.Count);

        var freePlan = seedData.FirstOrDefault(d => (string)d[nameof(SubscriptionPlan.Code)]! == "FREE");
        Assert.NotNull(freePlan);
        Assert.Equal(180, (int)freePlan[nameof(SubscriptionPlan.MonthlyLearningMinutes)]!);

        var premiumPlan = seedData.FirstOrDefault(d => (string)d[nameof(SubscriptionPlan.Code)]! == "PREMIUM");
        Assert.NotNull(premiumPlan);
        Assert.Equal(720, (int)premiumPlan[nameof(SubscriptionPlan.MonthlyLearningMinutes)]!);
    }
}
