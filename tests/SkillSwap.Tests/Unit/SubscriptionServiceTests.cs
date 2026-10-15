using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.DTOs.Subscriptions;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class SubscriptionServiceTests
{
    private (SubscriptionService Service, ApplicationDbContext Context, TestDateTimeProvider Dtp) CreateTestHarness()
    {
        var context = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString("N"));
        var dtp = new TestDateTimeProvider { UtcNow = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc) };

        context.SubscriptionPlans.AddRange(
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
        context.SaveChanges();

        var service = new SubscriptionService(context, dtp);
        return (service, context, dtp);
    }

    [Fact]
    public async Task GetActivePlans_ReturnsFreeAndPremiumPlansWithNullPremiumPrice()
    {
        var (service, _, _) = CreateTestHarness();

        var result = await service.GetActivePlansAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);

        var freePlan = result.Value.First(p => p.Code == "FREE");
        Assert.Equal(180, freePlan.MonthlyLearningMinutes);
        Assert.Equal(0.00m, freePlan.Price);

        var premiumPlan = result.Value.First(p => p.Code == "PREMIUM");
        Assert.Equal(720, premiumPlan.MonthlyLearningMinutes);
        Assert.Null(premiumPlan.Price); // Price remains null in MVP!
    }

    [Fact]
    public async Task GetCurrentSubscription_WhenActiveSubscriptionExists_ReturnsDto()
    {
        var (service, context, dtp) = CreateTestHarness();
        var userId = Guid.NewGuid();

        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Subscribed User" });

        var freePlan = await context.SubscriptionPlans.FirstAsync(p => p.Code == "FREE");
        context.UserSubscriptions.Add(new UserSubscription
        {
            UserId = userId,
            PlanId = freePlan.Id,
            StartsAtUtc = dtp.UtcNow.AddDays(-10),
            Status = SubscriptionStatus.Active,
            Plan = freePlan
        });
        await context.SaveChangesAsync();

        var result = await service.GetCurrentSubscriptionAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.Equal("FREE", result.Value!.PlanCode);
        Assert.Equal(180, result.Value.MonthlyLearningMinutes);
        Assert.Equal(SubscriptionStatus.Active, result.Value.Status);
    }

    [Fact]
    public async Task Subscribe_WhenUpgradingToPremium_CancelsOldAndActivatesNewPlan()
    {
        var (service, context, dtp) = CreateTestHarness();
        var userId = Guid.NewGuid();

        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Upgrader" });

        var freePlan = await context.SubscriptionPlans.FirstAsync(p => p.Code == "FREE");
        var oldSub = new UserSubscription
        {
            UserId = userId,
            PlanId = freePlan.Id,
            StartsAtUtc = dtp.UtcNow.AddDays(-30),
            Status = SubscriptionStatus.Active,
            Plan = freePlan
        };
        context.UserSubscriptions.Add(oldSub);
        await context.SaveChangesAsync();

        // Advance time for upgrade
        dtp.UtcNow = dtp.UtcNow.AddDays(1);

        var result = await service.SubscribeAsync(userId, new SubscribeRequest { PlanCode = "PREMIUM" });

        Assert.True(result.IsSuccess);
        Assert.Equal("PREMIUM", result.Value!.PlanCode);
        Assert.Equal(720, result.Value.MonthlyLearningMinutes);
        Assert.Equal(SubscriptionStatus.Active, result.Value.Status);
        Assert.Equal(dtp.UtcNow, result.Value.StartsAtUtc);

        // Verify old sub was cancelled
        var refreshedOld = await context.UserSubscriptions.FirstAsync(s => s.Id == oldSub.Id);
        Assert.Equal(SubscriptionStatus.Cancelled, refreshedOld.Status);
        Assert.Equal(dtp.UtcNow, refreshedOld.EndsAtUtc);

        // Verify only one active subscription exists for user
        var activeCount = await context.UserSubscriptions.CountAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active);
        Assert.Equal(1, activeCount);
    }

    [Fact]
    public async Task Subscribe_WhenAlreadyActiveOnSamePlan_IsIdempotent()
    {
        var (service, context, dtp) = CreateTestHarness();
        var userId = Guid.NewGuid();

        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "User" });

        var freePlan = await context.SubscriptionPlans.FirstAsync(p => p.Code == "FREE");
        var activeSub = new UserSubscription
        {
            UserId = userId,
            PlanId = freePlan.Id,
            StartsAtUtc = dtp.UtcNow.AddDays(-5),
            Status = SubscriptionStatus.Active,
            Plan = freePlan
        };
        context.UserSubscriptions.Add(activeSub);
        await context.SaveChangesAsync();

        var result = await service.SubscribeAsync(userId, new SubscribeRequest { PlanCode = "FREE" });

        Assert.True(result.IsSuccess);
        Assert.Equal("FREE", result.Value!.PlanCode);
        Assert.Equal(activeSub.Id, result.Value.Id);

        var activeCount = await context.UserSubscriptions.CountAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active);
        Assert.Equal(1, activeCount);
    }

    [Fact]
    public async Task Subscribe_UpdatesQuotaServiceLimitTo720Minutes()
    {
        var (service, context, dtp) = CreateTestHarness();
        var userId = Guid.NewGuid();

        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "User" });

        var freePlan = await context.SubscriptionPlans.FirstAsync(p => p.Code == "FREE");
        context.UserSubscriptions.Add(new UserSubscription
        {
            UserId = userId,
            PlanId = freePlan.Id,
            StartsAtUtc = dtp.UtcNow.AddDays(-5),
            Status = SubscriptionStatus.Active,
            Plan = freePlan
        });
        await context.SaveChangesAsync();

        var quotaService = new QuotaService(context);

        // Before upgrade: 180 min
        var limitBefore = await quotaService.GetMonthlyLimitMinutesAsync(userId, dtp.UtcNow);
        Assert.Equal(180, limitBefore);

        // Upgrade to Premium
        var subResult = await service.SubscribeAsync(userId, new SubscribeRequest { PlanCode = "PREMIUM" });
        Assert.True(subResult.IsSuccess);

        // After upgrade: 720 min!
        var limitAfter = await quotaService.GetMonthlyLimitMinutesAsync(userId, dtp.UtcNow);
        Assert.Equal(720, limitAfter);
    }
}
