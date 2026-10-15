using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common.Options;
using SkillSwap.Application.DTOs.Auth;
using SkillSwap.Application.DTOs.Availability;
using SkillSwap.Application.DTOs.Reviews;
using SkillSwap.Application.DTOs.Sessions;
using SkillSwap.Application.DTOs.Skills;
using SkillSwap.Application.DTOs.SwapRequests;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Identity;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Infrastructure.Services;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Integration;

public class EndToEndUserJourneyIntegrationTests
{
    private class TestHarness : IAsyncDisposable
    {
        public ApplicationDbContext Context { get; }
        public IServiceProvider ServiceProvider { get; }
        public TestDateTimeProvider Dtp { get; }
        public AuthService AuthService { get; }
        public SkillService SkillService { get; }
        public AvailabilityService AvailabilityService { get; }
        public MatchingService MatchingService { get; }
        public SwapRequestService SwapRequestService { get; }
        public ChatService ChatService { get; }
        public SessionBookingService BookingService { get; }
        public SessionCompletionService CompletionService { get; }
        public ReviewService ReviewService { get; }
        public NotificationService NotificationService { get; }

        public TestHarness(ApplicationDbContext context)
        {
            Context = context;
            Dtp = new TestDateTimeProvider { UtcNow = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc) };

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(Context);
            services.AddSingleton<IApplicationDbContext>(Context);

            services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            var jwtSettings = new JwtSettings
            {
                SecretKey = "a_super_secret_test_key_for_signing_tokens_with_256_bits_length!",
                Issuer = "SkillSwap.Test",
                Audience = "SkillSwap.TestAudience",
                ExpiryMinutes = 60
            };
            var jwtOptions = Options.Create(jwtSettings);
            services.AddSingleton(jwtOptions);
            services.AddSingleton<IDateTimeProvider>(Dtp);
            var jwtTokenService = new JwtTokenService(jwtOptions);
            services.AddSingleton<IJwtTokenService>(jwtTokenService);

            ServiceProvider = services.BuildServiceProvider();

            var userManager = ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

            AuthService = new AuthService(
                userManager,
                roleManager,
                Context,
                jwtTokenService,
                Dtp,
                jwtOptions,
                NullLogger<AuthService>.Instance);
            SkillService = new SkillService(Context, Dtp);
            AvailabilityService = new AvailabilityService(Context, Dtp);
            MatchingService = new MatchingService(Context);
            var sessionOptions = Options.Create(new SessionPolicyOptions());
            SwapRequestService = new SwapRequestService(Context, Dtp, sessionOptions);
            ChatService = new ChatService(Context, Dtp);

            var walletLockService = new SqlWalletLockService(Context, Dtp);
            var quotaService = new QuotaService(Context);
            BookingService = new SessionBookingService(Context, Dtp, walletLockService, quotaService);

            CompletionService = new SessionCompletionService(Context, Dtp, walletLockService, sessionOptions, NullLogger<SessionCompletionService>.Instance);

            ReviewService = new ReviewService(Context, Dtp);
            NotificationService = new NotificationService(Context, Dtp);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
        }
    }

    [Fact]
    public async Task CompleteUserJourney_FromRegistrationToReview_ExecutesSuccessfullyOnSqlServer()
    {
        await using var harness = new TestHarness(TestDbContextFactory.CreateSqlServerContext());
        var context = harness.Context;
        var dtp = harness.Dtp;

        // Ensure roles & Free plan exist
        await IdentityRoleSeeder.SeedRolesAsync(harness.ServiceProvider);
        var freePlan = await context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == "FREE");
        if (freePlan == null)
        {
            context.SubscriptionPlans.Add(new SubscriptionPlan
            {
                Id = 1,
                Code = "FREE",
                Name = "Free",
                MonthlyLearningMinutes = 180,
                Price = 0.00m,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        // ====================================================================
        // STEP 1 & 2: Register & Authenticate two users (Teacher & Learner)
        // ====================================================================
        var uniqueTeacherEmail = $"teacher_{Guid.NewGuid():N}@skillswap.test";
        var uniqueLearnerEmail = $"learner_{Guid.NewGuid():N}@skillswap.test";

        var teacherReg = await harness.AuthService.RegisterAsync(new RegisterRequestDto
        {
            DisplayName = "Bob Teacher",
            Email = uniqueTeacherEmail,
            Password = "Password123!"
        });
        Assert.True(teacherReg.IsSuccess, teacherReg.Error);
        var teacherId = teacherReg.Value!.UserId;

        var learnerReg = await harness.AuthService.RegisterAsync(new RegisterRequestDto
        {
            DisplayName = "Alice Learner",
            Email = uniqueLearnerEmail,
            Password = "Password123!"
        });
        Assert.True(learnerReg.IsSuccess, learnerReg.Error);
        var learnerId = learnerReg.Value!.UserId;

        // Login both users & obtain usable JWT tokens
        var teacherLogin = await harness.AuthService.LoginAsync(new LoginRequestDto
        {
            Email = uniqueTeacherEmail,
            Password = "Password123!"
        });
        Assert.True(teacherLogin.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(teacherLogin.Value!.AccessToken));

        var learnerLogin = await harness.AuthService.LoginAsync(new LoginRequestDto
        {
            Email = uniqueLearnerEmail,
            Password = "Password123!"
        });
        Assert.True(learnerLogin.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(learnerLogin.Value!.AccessToken));

        // ====================================================================
        // STEP 3: Configure Teacher's Teach skill and availability
        // ====================================================================
        var category = new Category
        {
            Name = "Engineering_" + Guid.NewGuid().ToString("N")[..8],
            IsActive = true,
            CreatedAtUtc = dtp.UtcNow
        };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var skill = new Skill
        {
            CategoryId = category.Id,
            Name = "ASP.NET Core_" + Guid.NewGuid().ToString("N")[..8],
            IsActive = true,
            CreatedAtUtc = dtp.UtcNow
        };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        var addTeacherSkill = await harness.SkillService.AddUserSkillAsync(teacherId, new AddUserSkillRequest
        {
            SkillId = skill.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Advanced
        });
        Assert.True(addTeacherSkill.IsSuccess, addTeacherSkill.Error);

        var setAvailability = await harness.AvailabilityService.UpdateUserAvailabilityAsync(teacherId, new UpdateAvailabilityRequest
        {
            Slots =
            [
                new AvailabilitySlotRequest
                {
                    DayOfWeek = DayOfWeek.Monday,
                    StartTime = new TimeOnly(10, 0),
                    EndTime = new TimeOnly(18, 0)
                }
            ]
        });
        Assert.True(setAvailability.IsSuccess, setAvailability.Error);

        // ====================================================================
        // STEP 4: Configure Learner's corresponding Learn skill
        // ====================================================================
        var addLearnerSkill = await harness.SkillService.AddUserSkillAsync(learnerId, new AddUserSkillRequest
        {
            SkillId = skill.Id,
            Type = UserSkillType.Learn,
            Level = UserSkillLevel.Beginner
        });
        Assert.True(addLearnerSkill.IsSuccess, addLearnerSkill.Error);

        // ====================================================================
        // STEP 5: Verify matching returns the teacher
        // ====================================================================
        var matchesResult = await harness.MatchingService.GetMatchesAsync(learnerId, skillId: skill.Id);
        Assert.True(matchesResult.IsSuccess, matchesResult.Error);
        Assert.Contains(matchesResult.Value!, m => m.TeacherId == teacherId && m.SkillId == skill.Id);

        // ====================================================================
        // STEP 6: Create a Swap Request as the learner
        // ====================================================================
        var createSwapResult = await harness.SwapRequestService.CreateSwapRequestAsync(learnerId, new CreateSwapRequest
        {
            ReceiverId = teacherId,
            SkillId = skill.Id,
            Message = "Hi Bob, I would love to learn ASP.NET Core from you!"
        });
        Assert.True(createSwapResult.IsSuccess, createSwapResult.Error);
        var swapRequestId = createSwapResult.Value!.Id;
        Assert.Equal(SwapRequestStatus.Pending, createSwapResult.Value.Status);

        // Verify incoming notification for Teacher
        var teacherNotifs = await harness.NotificationService.GetUserNotificationsAsync(teacherId);
        Assert.True(teacherNotifs.IsSuccess);
        Assert.Contains(teacherNotifs.Value!, n => n.Type == NotificationKind.SwapRequest);

        // ====================================================================
        // STEP 7: Accept Swap Request as the teacher
        // ====================================================================
        var acceptResult = await harness.SwapRequestService.AcceptSwapRequestAsync(teacherId, swapRequestId);
        Assert.True(acceptResult.IsSuccess, acceptResult.Error);
        Assert.Equal(SwapRequestStatus.Accepted, acceptResult.Value!.Status);

        // Verify acceptance notification for Learner
        var learnerNotifs = await harness.NotificationService.GetUserNotificationsAsync(learnerId);
        Assert.True(learnerNotifs.IsSuccess);
        Assert.Contains(learnerNotifs.Value!, n => n.Type == NotificationKind.SwapRequest);

        // ====================================================================
        // STEP 8: Verify exactly one conversation exists with both participants
        // ====================================================================
        var conv = await context.Conversations
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.SwapRequestId == swapRequestId);
        Assert.NotNull(conv);
        Assert.Equal(2, conv.Participants.Count);
        Assert.Contains(conv.Participants, p => p.UserId == teacherId);
        Assert.Contains(conv.Participants, p => p.UserId == learnerId);

        // ====================================================================
        // STEP 9: Send and retrieve messages with participant authorization
        // ====================================================================
        var msg1Result = await harness.ChatService.SendMessageAsync(learnerId, conv.Id, "Hello Bob, thanks for accepting!");
        Assert.True(msg1Result.IsSuccess, msg1Result.Error);

        var msg2Result = await harness.ChatService.SendMessageAsync(teacherId, conv.Id, "Hi Alice, welcome! Let's book a session.");
        Assert.True(msg2Result.IsSuccess, msg2Result.Error);

        var messagesResult = await harness.ChatService.GetConversationMessagesAsync(learnerId, conv.Id);
        Assert.True(messagesResult.IsSuccess);
        Assert.Equal(2, messagesResult.Value!.Count);

        // Stranger authorization rejection
        var strangerId = Guid.NewGuid();
        var strangerMsg = await harness.ChatService.SendMessageAsync(strangerId, conv.Id, "Intruder message");
        Assert.False(strangerMsg.IsSuccess);

        // ====================================================================
        // STEP 10: Configure booking prerequisites & learner wallet credits
        // ====================================================================
        var learnerWallet = await context.Wallets.FirstAsync(w => w.UserId == learnerId);
        learnerWallet.AvailableMinutes += 60;
        context.CreditTransactions.Add(new CreditTransaction
        {
            WalletId = learnerId,
            Type = CreditTransactionType.Bonus,
            AvailableDelta = 60,
            HeldDelta = 0,
            CreatedAtUtc = dtp.UtcNow,
            Description = "Initial balance for session test"
        });
        await context.SaveChangesAsync();

        // ====================================================================
        // STEP 11: Book a Session through the actual booking workflow
        // ====================================================================
        var sessionStart = dtp.UtcNow.AddDays(2);
        var bookResult = await harness.BookingService.BookSessionAsync(learnerId, new BookSessionRequest(
            swapRequestId,
            sessionStart,
            60,
            SessionMode.Online,
            null,
            false,
            skill.Id));
        Assert.True(bookResult.IsSuccess, bookResult.Error);
        var sessionId = bookResult.Value!.Id;
        Assert.Equal(SessionStatus.Scheduled, bookResult.Value.Status);

        // ====================================================================
        // STEP 12: Verify learner wallet AvailableMinutes and HeldMinutes
        // ====================================================================
        var refreshedLearnerWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learnerId);
        Assert.Equal(0, refreshedLearnerWallet.AvailableMinutes);
        Assert.Equal(60, refreshedLearnerWallet.HeldMinutes);

        var refreshedTeacherWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == teacherId);
        Assert.Equal(0, refreshedTeacherWallet.AvailableMinutes);
        Assert.Equal(0, refreshedTeacherWallet.HeldMinutes);

        // ====================================================================
        // STEP 13: Complete the Session through the supported lifecycle
        // ====================================================================
        var completeResult = await harness.CompletionService.CompleteSessionAsync(sessionId, teacherId);
        Assert.True(completeResult.IsSuccess, completeResult.Error);
        Assert.Equal(SessionStatus.Completed, completeResult.Value!.Status);

        // ====================================================================
        // STEP 14: Verify ledger and both wallets reflect the correct transfer
        // ====================================================================
        var finalLearnerWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == learnerId);
        Assert.Equal(0, finalLearnerWallet.AvailableMinutes);
        Assert.Equal(0, finalLearnerWallet.HeldMinutes);

        var finalTeacherWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == teacherId);
        Assert.Equal(60, finalTeacherWallet.AvailableMinutes);
        Assert.Equal(0, finalTeacherWallet.HeldMinutes);

        var captureTx = await context.CreditTransactions.FirstOrDefaultAsync(ct => ct.SessionId == sessionId && ct.Type == CreditTransactionType.Capture);
        Assert.NotNull(captureTx);
        Assert.Equal(learnerId, captureTx.WalletId);
        Assert.Equal(-60, captureTx.HeldDelta);

        var earnTx = await context.CreditTransactions.FirstOrDefaultAsync(ct => ct.SessionId == sessionId && ct.Type == CreditTransactionType.Earn);
        Assert.NotNull(earnTx);
        Assert.Equal(teacherId, earnTx.WalletId);
        Assert.Equal(60, earnTx.AvailableDelta);

        // Verify SessionCompleted notifications
        var teacherCompletedNotif = await context.Notifications
            .AnyAsync(n => n.UserId == teacherId && n.Type == NotificationKind.SessionCompleted);
        Assert.True(teacherCompletedNotif);

        var learnerCompletedNotif = await context.Notifications
            .AnyAsync(n => n.UserId == learnerId && n.Type == NotificationKind.SessionCompleted);
        Assert.True(learnerCompletedNotif);

        // ====================================================================
        // STEP 15: Submit review by eligible participant
        // ====================================================================
        var reviewResult = await harness.ReviewService.CreateReviewAsync(learnerId, new CreateReviewRequest
        {
            SessionId = sessionId,
            Rating = 5,
            Comment = "Bob was an exceptional teacher! Very clear explanations."
        });
        Assert.True(reviewResult.IsSuccess, reviewResult.Error);
        Assert.Equal(5, reviewResult.Value!.Rating);

        // ====================================================================
        // STEP 16: Retrieve notifications and reviews
        // ====================================================================
        var reviewsSummary = await harness.ReviewService.GetUserReviewsAsync(teacherId);
        Assert.True(reviewsSummary.IsSuccess);
        Assert.Equal(1, reviewsSummary.Value!.TotalReviews);
        Assert.Equal(5.0, reviewsSummary.Value.AverageRating);

        var allTeacherNotifs = await harness.NotificationService.GetUserNotificationsAsync(teacherId);
        Assert.True(allTeacherNotifs.IsSuccess);
        var reviewNotif = allTeacherNotifs.Value!.FirstOrDefault(n => n.Type == NotificationKind.ReviewReceived);
        Assert.NotNull(reviewNotif);

        var markRead = await harness.NotificationService.MarkAsReadAsync(teacherId, reviewNotif.Id);
        Assert.True(markRead.IsSuccess);
    }
}
