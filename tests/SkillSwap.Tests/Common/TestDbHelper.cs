using Microsoft.EntityFrameworkCore;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Identity;
using SkillSwap.Infrastructure.Persistence;

namespace SkillSwap.Tests.Common;

public static class TestDbHelper
{
    public static async Task<(ApplicationUser Learner, ApplicationUser Teacher, Skill Skill, SwapRequest SwapReq)> CreateStandardBookingSetupAsync(
        ApplicationDbContext context,
        int learnerInitialAvailableMinutes = 180,
        int teacherInitialAvailableMinutes = 60)
    {
        var category = new Category
        {
            Name = "Cat_" + Guid.NewGuid().ToString("N")[..8],
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var skill = new Skill
        {
            CategoryId = category.Id,
            Name = "Skill_" + Guid.NewGuid().ToString("N")[..8],
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        var learner = await CreateUserAsync(context, "lrn_" + Guid.NewGuid().ToString("N")[..8]);
        var teacher = await CreateUserAsync(context, "tch_" + Guid.NewGuid().ToString("N")[..8]);

        var learnerWallet = new Wallet
        {
            UserId = learner.Id,
            AvailableMinutes = learnerInitialAvailableMinutes,
            HeldMinutes = 0,
            UpdatedAtUtc = DateTime.UtcNow
        };
        var teacherWallet = new Wallet
        {
            UserId = teacher.Id,
            AvailableMinutes = teacherInitialAvailableMinutes,
            HeldMinutes = 0,
            UpdatedAtUtc = DateTime.UtcNow
        };
        context.Wallets.AddRange(learnerWallet, teacherWallet);

        var teacherSkill = new UserSkill
        {
            UserId = teacher.Id,
            SkillId = skill.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Intermediate,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.UserSkills.Add(teacherSkill);

        var swapRequest = new SwapRequest
        {
            RequesterId = learner.Id,
            ReceiverId = teacher.Id,
            SkillId = skill.Id,
            Status = SwapRequestStatus.Accepted,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            RespondedAtUtc = DateTime.UtcNow.AddHours(-12)
        };
        context.SwapRequests.Add(swapRequest);

        await context.SaveChangesAsync();

        return (learner, teacher, skill, swapRequest);
    }

    public static async Task<ApplicationUser> CreateUserAsync(ApplicationDbContext context, string username, string? plan = "Free")
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            Email = $"{username}@skillswap.test",
            NormalizedEmail = $"{username.ToUpperInvariant()}@SKILLSWAP.TEST",
            SecurityStamp = Guid.NewGuid().ToString()
        };
        context.Users.Add(user);

        var profile = new UserProfile
        {
            UserId = user.Id,
            DisplayName = username,
            Bio = "Test user profile bio",
            TimeZoneId = "UTC",
            IsVerified = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.UserProfiles.Add(profile);

        if (plan != null)
        {
            var planCode = plan.ToUpperInvariant();
            var dbPlan = await context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == planCode);
            if (dbPlan == null)
            {
                dbPlan = new SubscriptionPlan
                {
                    Code = planCode,
                    Name = plan,
                    Price = plan == "Free" ? 0.00m : null,
                    MonthlyLearningMinutes = plan == "Free" ? 180 : 720,
                    IsActive = true
                };
                context.SubscriptionPlans.Add(dbPlan);
                await context.SaveChangesAsync();
            }

            var sub = new UserSubscription
            {
                UserId = user.Id,
                PlanId = dbPlan.Id,
                Status = SubscriptionStatus.Active,
                StartsAtUtc = DateTime.UtcNow.AddDays(-5),
                EndsAtUtc = DateTime.UtcNow.AddDays(25),
                CreatedAtUtc = DateTime.UtcNow
            };
            context.UserSubscriptions.Add(sub);
        }

        await context.SaveChangesAsync();
        return user;
    }
}

