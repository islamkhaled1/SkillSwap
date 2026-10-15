using Microsoft.Extensions.Options;
using SkillSwap.Application.Common.Options;
using SkillSwap.Application.DTOs.SwapRequests;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class SwapRequestServiceTests
{
    private (SwapRequestService Service, ApplicationDbContext Context, TestDateTimeProvider Dtp) CreateTestHarness()
    {
        var context = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString("N"));
        var dtp = new TestDateTimeProvider { UtcNow = new DateTime(2026, 10, 10, 10, 0, 0, DateTimeKind.Utc) };
        var options = Options.Create(new SessionPolicyOptions { OldSwapRequestExpirationDays = 7 });

        var defaultCategory = new Category { Id = 1, Name = "General", IsActive = true };
        var defaultSkill = new Skill { Id = 1, CategoryId = 1, Name = "General Skill", IsActive = true };
        context.Categories.Add(defaultCategory);
        context.Skills.Add(defaultSkill);
        context.SaveChanges();

        var service = new SwapRequestService(context, dtp, options);
        return (service, context, dtp);
    }

    [Fact]
    public async Task CreateSwapRequest_ValidProposal_CreatesPendingRequestWith7DaysExpiration()
    {
        var (service, context, dtp) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = learnerId, DisplayName = "Learner User" },
            new UserProfile { UserId = teacherId, DisplayName = "Teacher User" }
        );

        var cat = new Category { Name = "Programming", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "C#", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        context.UserSkills.Add(new UserSkill
        {
            UserId = teacherId,
            SkillId = skill.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Advanced
        });
        await context.SaveChangesAsync();

        var request = new CreateSwapRequest
        {
            ReceiverId = teacherId,
            SkillId = skill.Id,
            Message = "Hey, I would love to learn C# from you!"
        };

        var result = await service.CreateSwapRequestAsync(learnerId, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(learnerId, result.Value.RequesterId);
        Assert.Equal(teacherId, result.Value.ReceiverId);
        Assert.Equal(skill.Id, result.Value.SkillId);
        Assert.Equal("C#", result.Value.SkillName);
        Assert.Equal(SwapRequestStatus.Pending, result.Value.Status);
        Assert.Equal("Hey, I would love to learn C# from you!", result.Value.Message);
        Assert.Equal(dtp.UtcNow, result.Value.CreatedAtUtc);
        Assert.Equal(dtp.UtcNow.AddDays(7), result.Value.ExpiresAtUtc);
    }

    [Fact]
    public async Task CreateSwapRequest_SelfSwap_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();

        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Self" });
        await context.SaveChangesAsync();

        var request = new CreateSwapRequest
        {
            ReceiverId = userId,
            SkillId = 1
        };

        var result = await service.CreateSwapRequestAsync(userId, request);

        Assert.True(result.IsFailure);
        Assert.Contains("yourself", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSwapRequest_WhenTeacherDoesNotTeachSkill_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = learnerId, DisplayName = "Learner" },
            new UserProfile { UserId = teacherId, DisplayName = "Teacher" }
        );

        var cat = new Category { Name = "Music", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "Piano", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        // Teacher does NOT have piano in UserSkills
        var request = new CreateSwapRequest
        {
            ReceiverId = teacherId,
            SkillId = skill.Id
        };

        var result = await service.CreateSwapRequestAsync(learnerId, request);

        Assert.True(result.IsFailure);
        Assert.Contains("not an active teacher", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSwapRequest_WhenUsersBlockedInEitherDirection_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = learnerId, DisplayName = "Learner" },
            new UserProfile { UserId = teacherId, DisplayName = "Teacher" }
        );

        var cat = new Category { Name = "Tech", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "Python", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        context.UserSkills.Add(new UserSkill { UserId = teacherId, SkillId = skill.Id, Type = UserSkillType.Teach });

        // Teacher blocked learner
        context.UserBlocks.Add(new UserBlock { BlockerId = teacherId, BlockedId = learnerId });
        await context.SaveChangesAsync();

        var request = new CreateSwapRequest { ReceiverId = teacherId, SkillId = skill.Id };
        var result = await service.CreateSwapRequestAsync(learnerId, request);

        Assert.True(result.IsFailure);
        Assert.Contains("block", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSwapRequest_DuplicatePendingRequest_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = learnerId, DisplayName = "Learner" },
            new UserProfile { UserId = teacherId, DisplayName = "Teacher" }
        );

        var cat = new Category { Name = "Tech", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "Go", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        context.UserSkills.Add(new UserSkill { UserId = teacherId, SkillId = skill.Id, Type = UserSkillType.Teach });
        await context.SaveChangesAsync();

        var request = new CreateSwapRequest { ReceiverId = teacherId, SkillId = skill.Id };

        var first = await service.CreateSwapRequestAsync(learnerId, request);
        Assert.True(first.IsSuccess);

        var second = await service.CreateSwapRequestAsync(learnerId, request);
        Assert.True(second.IsFailure);
        Assert.Contains("already exists", second.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcceptSwapRequest_ByTeacher_TransitionsToAcceptedAndCreatesConversationWithParticipants()
    {
        var (service, context, dtp) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = learnerId, DisplayName = "Learner" },
            new UserProfile { UserId = teacherId, DisplayName = "Teacher" }
        );

        var cat = new Category { Name = "Languages", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "Spanish", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        var swapRequest = new SwapRequest
        {
            RequesterId = learnerId,
            ReceiverId = teacherId,
            SkillId = skill.Id,
            Status = SwapRequestStatus.Pending,
            CreatedAtUtc = dtp.UtcNow.AddDays(-1),
            ExpiresAtUtc = dtp.UtcNow.AddDays(6)
        };
        context.SwapRequests.Add(swapRequest);
        await context.SaveChangesAsync();

        var result = await service.AcceptSwapRequestAsync(teacherId, swapRequest.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(SwapRequestStatus.Accepted, result.Value!.Status);
        Assert.NotNull(result.Value.ConversationId);
        Assert.Equal(dtp.UtcNow, result.Value.RespondedAtUtc);

        // Verify conversation was created with both participants
        var conversation = context.Conversations.FirstOrDefault(c => c.SwapRequestId == swapRequest.Id);
        Assert.NotNull(conversation);

        var participants = context.ConversationParticipants.Where(cp => cp.ConversationId == conversation.Id).ToList();
        Assert.Equal(2, participants.Count);
        Assert.Contains(participants, p => p.UserId == learnerId);
        Assert.Contains(participants, p => p.UserId == teacherId);
    }

    [Fact]
    public async Task AcceptSwapRequest_ConcurrentAcceptance_PreventsDuplicateConversation()
    {
        var (service, context, dtp) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var swapRequest = new SwapRequest
        {
            RequesterId = learnerId,
            ReceiverId = teacherId,
            SkillId = 1,
            Status = SwapRequestStatus.Pending,
            CreatedAtUtc = dtp.UtcNow,
            ExpiresAtUtc = dtp.UtcNow.AddDays(7)
        };
        context.SwapRequests.Add(swapRequest);
        await context.SaveChangesAsync();

        // First acceptance succeeds
        var first = await service.AcceptSwapRequestAsync(teacherId, swapRequest.Id);
        Assert.True(first.IsSuccess);

        // Second acceptance fails and does not create another conversation
        var second = await service.AcceptSwapRequestAsync(teacherId, swapRequest.Id);
        Assert.True(second.IsFailure);

        var conversationsCount = context.Conversations.Count(c => c.SwapRequestId == swapRequest.Id);
        Assert.Equal(1, conversationsCount);
    }

    [Fact]
    public async Task AcceptSwapRequest_ByNonReceiver_ReturnsFailure()
    {
        var (service, context, dtp) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        var swapRequest = new SwapRequest
        {
            RequesterId = learnerId,
            ReceiverId = teacherId,
            SkillId = 1,
            Status = SwapRequestStatus.Pending,
            CreatedAtUtc = dtp.UtcNow,
            ExpiresAtUtc = dtp.UtcNow.AddDays(7)
        };
        context.SwapRequests.Add(swapRequest);
        await context.SaveChangesAsync();

        // Learner cannot accept (only receiver can accept)
        var learnerAttempt = await service.AcceptSwapRequestAsync(learnerId, swapRequest.Id);
        Assert.True(learnerAttempt.IsFailure);
        Assert.Contains("Only the receiver", learnerAttempt.Error, StringComparison.OrdinalIgnoreCase);

        // Third-party attacker cannot accept
        var attackerAttempt = await service.AcceptSwapRequestAsync(attackerId, swapRequest.Id);
        Assert.True(attackerAttempt.IsFailure);
        Assert.Contains("Only the receiver", attackerAttempt.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcceptSwapRequest_WhenNotPending_ReturnsFailure()
    {
        var (service, context, dtp) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var swapRequest = new SwapRequest
        {
            RequesterId = learnerId,
            ReceiverId = teacherId,
            SkillId = 1,
            Status = SwapRequestStatus.Declined,
            CreatedAtUtc = dtp.UtcNow,
            RespondedAtUtc = dtp.UtcNow
        };
        context.SwapRequests.Add(swapRequest);
        await context.SaveChangesAsync();

        var result = await service.AcceptSwapRequestAsync(teacherId, swapRequest.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("Pending status", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcceptSwapRequest_WhenExpired_MarksExpiredAndReturnsFailure()
    {
        var (service, context, dtp) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var swapRequest = new SwapRequest
        {
            RequesterId = learnerId,
            ReceiverId = teacherId,
            SkillId = 1,
            Status = SwapRequestStatus.Pending,
            CreatedAtUtc = dtp.UtcNow.AddDays(-10),
            ExpiresAtUtc = dtp.UtcNow.AddDays(-3) // Expired 3 days ago
        };
        context.SwapRequests.Add(swapRequest);
        await context.SaveChangesAsync();

        var result = await service.AcceptSwapRequestAsync(teacherId, swapRequest.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("expired", result.Error, StringComparison.OrdinalIgnoreCase);

        // Status updated to Expired
        var updated = context.SwapRequests.Find(swapRequest.Id);
        Assert.Equal(SwapRequestStatus.Expired, updated!.Status);
    }

    [Fact]
    public async Task DeclineSwapRequest_ByTeacher_TransitionsToDeclinedWithoutConversation()
    {
        var (service, context, dtp) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var swapRequest = new SwapRequest
        {
            RequesterId = learnerId,
            ReceiverId = teacherId,
            SkillId = 1,
            Status = SwapRequestStatus.Pending,
            CreatedAtUtc = dtp.UtcNow,
            ExpiresAtUtc = dtp.UtcNow.AddDays(7)
        };
        context.SwapRequests.Add(swapRequest);
        await context.SaveChangesAsync();

        var result = await service.DeclineSwapRequestAsync(teacherId, swapRequest.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(SwapRequestStatus.Declined, result.Value!.Status);
        Assert.Null(result.Value.ConversationId);

        // Verify NO conversation created
        Assert.Empty(context.Conversations.Where(c => c.SwapRequestId == swapRequest.Id));
    }

    [Fact]
    public async Task CancelSwapRequest_ByLearner_TransitionsToCancelledWithoutConversation()
    {
        var (service, context, dtp) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var swapRequest = new SwapRequest
        {
            RequesterId = learnerId,
            ReceiverId = teacherId,
            SkillId = 1,
            Status = SwapRequestStatus.Pending,
            CreatedAtUtc = dtp.UtcNow,
            ExpiresAtUtc = dtp.UtcNow.AddDays(7)
        };
        context.SwapRequests.Add(swapRequest);
        await context.SaveChangesAsync();

        var result = await service.CancelSwapRequestAsync(learnerId, swapRequest.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(SwapRequestStatus.Cancelled, result.Value!.Status);
        Assert.NotNull(result.Value.CancelledAtUtc);

        // Verify NO conversation created
        Assert.Empty(context.Conversations.Where(c => c.SwapRequestId == swapRequest.Id));
    }

    [Fact]
    public async Task CancelSwapRequest_ByTeacher_ReturnsFailure()
    {
        var (service, context, dtp) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        var swapRequest = new SwapRequest
        {
            RequesterId = learnerId,
            ReceiverId = teacherId,
            SkillId = 1,
            Status = SwapRequestStatus.Pending,
            CreatedAtUtc = dtp.UtcNow,
            ExpiresAtUtc = dtp.UtcNow.AddDays(7)
        };
        context.SwapRequests.Add(swapRequest);
        await context.SaveChangesAsync();

        var result = await service.CancelSwapRequestAsync(teacherId, swapRequest.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("Only the requester", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSwapRequestById_WhenUnrelatedUser_ReturnsFailure()
    {
        var (service, context, dtp) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var bystanderId = Guid.NewGuid();

        var swapRequest = new SwapRequest
        {
            RequesterId = learnerId,
            ReceiverId = teacherId,
            SkillId = 1,
            Status = SwapRequestStatus.Pending,
            CreatedAtUtc = dtp.UtcNow,
            ExpiresAtUtc = dtp.UtcNow.AddDays(7)
        };
        context.SwapRequests.Add(swapRequest);
        await context.SaveChangesAsync();

        var result = await service.GetSwapRequestByIdAsync(bystanderId, swapRequest.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("not authorized", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExpireOldRequests_ExpiresPastPendingRequests()
    {
        var (service, context, dtp) = CreateTestHarness();

        var past1 = new SwapRequest
        {
            RequesterId = Guid.NewGuid(),
            ReceiverId = Guid.NewGuid(),
            SkillId = 1,
            Status = SwapRequestStatus.Pending,
            ExpiresAtUtc = dtp.UtcNow.AddMinutes(-5)
        };
        var past2 = new SwapRequest
        {
            RequesterId = Guid.NewGuid(),
            ReceiverId = Guid.NewGuid(),
            SkillId = 2,
            Status = SwapRequestStatus.Pending,
            ExpiresAtUtc = dtp.UtcNow.AddDays(-1)
        };
        var future = new SwapRequest
        {
            RequesterId = Guid.NewGuid(),
            ReceiverId = Guid.NewGuid(),
            SkillId = 3,
            Status = SwapRequestStatus.Pending,
            ExpiresAtUtc = dtp.UtcNow.AddDays(3)
        };

        context.SwapRequests.AddRange(past1, past2, future);
        await context.SaveChangesAsync();

        var countResult = await service.ExpireOldRequestsAsync();

        Assert.True(countResult.IsSuccess);
        Assert.Equal(2, countResult.Value);
        Assert.Equal(SwapRequestStatus.Expired, context.SwapRequests.Find(past1.Id)!.Status);
        Assert.Equal(SwapRequestStatus.Expired, context.SwapRequests.Find(past2.Id)!.Status);
        Assert.Equal(SwapRequestStatus.Pending, context.SwapRequests.Find(future.Id)!.Status);
    }
}
