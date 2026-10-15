using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkillSwap.Application.DTOs.Auth;
using SkillSwap.Application.DTOs.Availability;
using SkillSwap.Application.DTOs.Notifications;
using SkillSwap.Application.DTOs.Reviews;
using SkillSwap.Application.DTOs.Sessions;
using SkillSwap.Application.DTOs.Skills;
using SkillSwap.Application.DTOs.SwapRequests;
using SkillSwap.Application.DTOs.Wallets;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Persistence;

namespace SkillSwap.Tests.Integration;

[Collection("HostStartupCollection")]
public class HttpFinancialJourneyIntegrationTests : IClassFixture<HttpFinancialJourneyIntegrationTests.CustomFactory>
{
    public class CustomFactory : WebApplicationFactory<Program>
    {
        public string AdminEmail { get; } = $"admin_journey_{Guid.NewGuid().ToString("N")[..8]}@skillswap.test";
        public string AdminPassword { get; } = "AdminPassword123!";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=SkillSwap_IntegrationTestDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;",
                    ["AdminUser:Email"] = AdminEmail,
                    ["AdminUser:Password"] = AdminPassword,
                    ["AdminUser:DisplayName"] = "Integration Test Administrator"
                });
            });
        }
    }

    private readonly CustomFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public HttpFinancialJourneyIntegrationTests(CustomFactory factory)
    {
        _factory = factory;
    }

    private static void SetBearerToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task CompleteFinancialJourney_ViaRealHttpPipeline_EnforcesAuthorizationAndLedgerInvariants()
    {
        var client = _factory.CreateClient();

        // Ensure database seed prerequisites (Free plan, Category, Skill) exist in SkillSwap_IntegrationTestDb
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!await db.SubscriptionPlans.AnyAsync(p => p.Code == "FREE"))
            {
                db.SubscriptionPlans.Add(new SubscriptionPlan
                {
                    Id = 1,
                    Code = "FREE",
                    Name = "Free",
                    MonthlyLearningMinutes = 180,
                    Price = 0.00m,
                    IsActive = true
                });
                await db.SaveChangesAsync();
            }

            if (!await db.Categories.AnyAsync())
            {
                var cat = new Category { Name = "Engineering_" + Guid.NewGuid().ToString("N")[..6], IsActive = true, CreatedAtUtc = DateTime.UtcNow };
                db.Categories.Add(cat);
                await db.SaveChangesAsync();

                var sk = new Skill { CategoryId = cat.Id, Name = "C# .NET_" + Guid.NewGuid().ToString("N")[..6], IsActive = true, CreatedAtUtc = DateTime.UtcNow };
                db.Skills.Add(sk);
                await db.SaveChangesAsync();
            }
        }

        int activeSkillId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            activeSkillId = (await db.Skills.FirstAsync(s => s.IsActive)).Id;
        }

        // ---------------------------------------------------------------------
        // 1. Authenticate Admin (provisioned automatically via environment/config)
        // ---------------------------------------------------------------------
        var adminLoginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = _factory.AdminEmail,
            Password = _factory.AdminPassword
        });
        Assert.Equal(HttpStatusCode.OK, adminLoginRes.StatusCode);
        var adminAuth = await adminLoginRes.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        Assert.NotNull(adminAuth);
        var adminToken = adminAuth.AccessToken;

        // ---------------------------------------------------------------------
        // 2. Register & Authenticate Learner and Teacher
        // ---------------------------------------------------------------------
        var teacherEmail = $"teacher_hfj_{Guid.NewGuid().ToString("N")[..8]}@skillswap.test";
        var learnerEmail = $"learner_hfj_{Guid.NewGuid().ToString("N")[..8]}@skillswap.test";

        var teacherRegRes = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            DisplayName = "Teacher Bob",
            Email = teacherEmail,
            Password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.Created, teacherRegRes.StatusCode);
        var teacherAuth = await teacherRegRes.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        Assert.NotNull(teacherAuth);
        var teacherId = teacherAuth.UserId;
        var teacherToken = teacherAuth.AccessToken;

        var learnerRegRes = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            DisplayName = "Learner Alice",
            Email = learnerEmail,
            Password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.Created, learnerRegRes.StatusCode);
        var learnerAuth = await learnerRegRes.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        Assert.NotNull(learnerAuth);
        var learnerId = learnerAuth.UserId;
        var learnerToken = learnerAuth.AccessToken;

        // ---------------------------------------------------------------------
        // 3. Authorization Check: Regular users cannot grant themselves credits
        // ---------------------------------------------------------------------
        using var learnerClient = _factory.CreateClient();
        SetBearerToken(learnerClient, learnerToken);

        var unauthorizedGrantRes = await learnerClient.PostAsJsonAsync($"/api/admin/wallets/{learnerId}/bonus", new GrantBonusRequest
        {
            Minutes = 60,
            Description = "Self-grant attempt"
        });
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedGrantRes.StatusCode);

        // ---------------------------------------------------------------------
        // 4. Admin grants initial bonus credits to Learner (with Idempotency-Key)
        // ---------------------------------------------------------------------
        using var adminClient = _factory.CreateClient();
        SetBearerToken(adminClient, adminToken);

        var bonusKey = "journey-bonus-" + Guid.NewGuid().ToString("N")[..8];
        var grantReq = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/wallets/{learnerId}/bonus")
        {
            Content = JsonContent.Create(new GrantBonusRequest
            {
                Minutes = 60,
                Description = "Approved initial learning credits"
            })
        };
        grantReq.Headers.Add("Idempotency-Key", bonusKey);

        var grantRes = await adminClient.SendAsync(grantReq);
        Assert.Equal(HttpStatusCode.OK, grantRes.StatusCode);
        var bonusTx = await grantRes.Content.ReadFromJsonAsync<CreditTransactionDto>(JsonOptions);
        Assert.NotNull(bonusTx);
        Assert.Equal(CreditTransactionType.Bonus, bonusTx.Type);
        Assert.Equal(60, bonusTx.AvailableDelta);

        // Replay with identical key must succeed idempotently without double-granting
        var replayReq = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/wallets/{learnerId}/bonus")
        {
            Content = JsonContent.Create(new GrantBonusRequest
            {
                Minutes = 60,
                Description = "Approved initial learning credits"
            })
        };
        replayReq.Headers.Add("Idempotency-Key", bonusKey);
        var replayRes = await adminClient.SendAsync(replayReq);
        Assert.Equal(HttpStatusCode.OK, replayRes.StatusCode);

        // Verify Learner wallet reflection via HTTP
        var learnerWalletRes = await learnerClient.GetFromJsonAsync<WalletDto>("/api/wallets/me", JsonOptions);
        Assert.NotNull(learnerWalletRes);
        Assert.Equal(60, learnerWalletRes.AvailableMinutes);
        Assert.Equal(0, learnerWalletRes.HeldMinutes);

        // ---------------------------------------------------------------------
        // 5. Configure Skills, Availability & Accept Swap Request
        // ---------------------------------------------------------------------
        using var teacherClient = _factory.CreateClient();
        SetBearerToken(teacherClient, teacherToken);

        var addTeacherSkillRes = await teacherClient.PostAsJsonAsync("/api/users/me/skills", new AddUserSkillRequest
        {
            SkillId = activeSkillId,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Advanced
        });
        Assert.Equal(HttpStatusCode.Created, addTeacherSkillRes.StatusCode);

        var setAvailRes = await teacherClient.PutAsJsonAsync("/api/users/me/availability", new UpdateAvailabilityRequest
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
        Assert.Equal(HttpStatusCode.OK, setAvailRes.StatusCode);

        var addLearnerSkillRes = await learnerClient.PostAsJsonAsync("/api/users/me/skills", new AddUserSkillRequest
        {
            SkillId = activeSkillId,
            Type = UserSkillType.Learn,
            Level = UserSkillLevel.Beginner
        });
        Assert.Equal(HttpStatusCode.Created, addLearnerSkillRes.StatusCode);

        // Propose and accept swap request
        var swapReqRes = await learnerClient.PostAsJsonAsync("/api/swap-requests", new CreateSwapRequest
        {
            ReceiverId = teacherId,
            SkillId = activeSkillId,
            Message = "Let's learn!"
        });
        Assert.Equal(HttpStatusCode.Created, swapReqRes.StatusCode);
        var swapReq = await swapReqRes.Content.ReadFromJsonAsync<SwapRequestDto>(JsonOptions);
        Assert.NotNull(swapReq);

        var acceptRes = await teacherClient.PostAsync($"/api/swap-requests/{swapReq.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptRes.StatusCode);

        // ---------------------------------------------------------------------
        // 6. Book Session via HTTP (Learner books 60-min session)
        // ---------------------------------------------------------------------
        var sessionStart = DateTime.UtcNow.AddDays(2);
        var bookRes = await learnerClient.PostAsJsonAsync("/api/sessions/book", new BookSessionRequest(
            swapReq.Id,
            sessionStart,
            60,
            SessionMode.Online,
            null,
            false,
            activeSkillId));
        Assert.Equal(HttpStatusCode.Created, bookRes.StatusCode);
        var bookedSession = await bookRes.Content.ReadFromJsonAsync<SessionDto>(JsonOptions);
        Assert.NotNull(bookedSession);
        var sessionId = bookedSession.Id;

        // Verify Learner wallet holds 60 minutes
        learnerWalletRes = await learnerClient.GetFromJsonAsync<WalletDto>("/api/wallets/me", JsonOptions);
        Assert.NotNull(learnerWalletRes);
        Assert.Equal(0, learnerWalletRes.AvailableMinutes);
        Assert.Equal(60, learnerWalletRes.HeldMinutes);

        // ---------------------------------------------------------------------
        // 7. Security: Unauthorized stranger cannot complete session
        // ---------------------------------------------------------------------
        var strangerEmail = $"stranger_{Guid.NewGuid().ToString("N")[..8]}@skillswap.test";
        var strangerReg = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            DisplayName = "Stranger",
            Email = strangerEmail,
            Password = "Password123!"
        });
        var strangerAuth = await strangerReg.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        using var strangerClient = _factory.CreateClient();
        SetBearerToken(strangerClient, strangerAuth!.AccessToken);

        var strangerCompleteRes = await strangerClient.PostAsync($"/api/sessions/{sessionId}/complete", null);
        Assert.Equal(HttpStatusCode.Forbidden, strangerCompleteRes.StatusCode);

        // ---------------------------------------------------------------------
        // 8. Complete Session via HTTP by Teacher
        // ---------------------------------------------------------------------
        var completeRes = await teacherClient.PostAsync($"/api/sessions/{sessionId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completeRes.StatusCode);
        var completedSession = await completeRes.Content.ReadFromJsonAsync<SessionDto>(JsonOptions);
        Assert.NotNull(completedSession);
        Assert.Equal(SessionStatus.Completed, completedSession.Status);

        // ---------------------------------------------------------------------
        // 9. Verify Post-Completion Wallets & Ledger via HTTP
        // ---------------------------------------------------------------------
        learnerWalletRes = await learnerClient.GetFromJsonAsync<WalletDto>("/api/wallets/me", JsonOptions);
        Assert.NotNull(learnerWalletRes);
        Assert.Equal(0, learnerWalletRes.AvailableMinutes);
        Assert.Equal(0, learnerWalletRes.HeldMinutes);

        var teacherWalletRes = await teacherClient.GetFromJsonAsync<WalletDto>("/api/wallets/me", JsonOptions);
        Assert.NotNull(teacherWalletRes);
        Assert.Equal(60, teacherWalletRes.AvailableMinutes);
        Assert.Equal(0, teacherWalletRes.HeldMinutes);

        var learnerTxs = await learnerClient.GetFromJsonAsync<IReadOnlyList<CreditTransactionDto>>("/api/wallets/me/transactions", JsonOptions);
        Assert.NotNull(learnerTxs);
        Assert.Contains(learnerTxs, tx => tx.Type == CreditTransactionType.Capture && tx.HeldDelta == -60);

        var teacherTxs = await teacherClient.GetFromJsonAsync<IReadOnlyList<CreditTransactionDto>>("/api/wallets/me/transactions", JsonOptions);
        Assert.NotNull(teacherTxs);
        Assert.Contains(teacherTxs, tx => tx.Type == CreditTransactionType.Earn && tx.AvailableDelta == 60);

        // ---------------------------------------------------------------------
        // 10. Review Submission & Canonical Review Route Verification
        // ---------------------------------------------------------------------
        var createReviewRes = await learnerClient.PostAsJsonAsync("/api/reviews", new CreateReviewRequest
        {
            SessionId = sessionId,
            Rating = 5,
            Comment = "Phenomenal teaching session, learned immensely!"
        });
        Assert.Equal(HttpStatusCode.Created, createReviewRes.StatusCode);

        // Verify public reviews via Canonical Route: GET /api/users/{id}/reviews
        var canonicalReviewRes = await client.GetFromJsonAsync<UserReviewsSummaryDto>($"/api/users/{teacherId}/reviews", JsonOptions);
        Assert.NotNull(canonicalReviewRes);
        Assert.Equal(1, canonicalReviewRes.TotalReviews);
        Assert.Equal(5.0, canonicalReviewRes.AverageRating);

        // ---------------------------------------------------------------------
        // 11. Notifications Verification
        // ---------------------------------------------------------------------
        var teacherNotifs = await teacherClient.GetFromJsonAsync<IReadOnlyList<NotificationDto>>("/api/notifications", JsonOptions);
        Assert.NotNull(teacherNotifs);
        Assert.Contains(teacherNotifs, n => n.Type == NotificationKind.SessionCompleted);
        Assert.Contains(teacherNotifs, n => n.Type == NotificationKind.ReviewReceived);
    }
}
