using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Auth;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Identity;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Infrastructure.Services;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class AuthServiceTests
{
    private (AuthService Service, ApplicationDbContext Context, UserManager<ApplicationUser> UserManager, RoleManager<IdentityRole<Guid>> RoleManager, TestDateTimeProvider Dtp)
        CreateTestHarness(string? dbName = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());

        var contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new ApplicationDbContext(contextOptions);
        services.AddScoped(_ => context);

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
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

        var dtp = new TestDateTimeProvider { UtcNow = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc) };
        services.AddSingleton<IDateTimeProvider>(dtp);

        var jwtTokenService = new JwtTokenService(jwtOptions);
        services.AddSingleton<IJwtTokenService>(jwtTokenService);

        var sp = services.BuildServiceProvider();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var logger = sp.GetRequiredService<ILogger<AuthService>>();

        // Seed default FREE plan
        if (!context.SubscriptionPlans.Any(sp => sp.Code == "FREE"))
        {
            context.SubscriptionPlans.Add(new SubscriptionPlan
            {
                Id = 1,
                Code = "FREE",
                Name = "Free",
                MonthlyLearningMinutes = 180,
                Price = 0.00m,
                PriorityMatching = false,
                AdvancedSearch = false,
                IsActive = true
            });
            context.SaveChanges();
        }

        var authService = new AuthService(
            userManager,
            roleManager,
            context,
            jwtTokenService,
            dtp,
            jwtOptions,
            logger);

        return (authService, context, userManager, roleManager, dtp);
    }

    [Fact]
    public async Task Register_WithValidData_CreatesAllRequiredRecords_WithZeroWallet_AndFreePlan()
    {
        var (authService, context, userManager, _, dtp) = CreateTestHarness();
        var request = new RegisterRequestDto
        {
            Email = "newuser@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Test User"
        };

        var result = await authService.RegisterAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("newuser@example.com", result.Value.Email);
        Assert.NotEmpty(result.Value.AccessToken);
        Assert.Equal(dtp.UtcNow.AddMinutes(60), result.Value.ExpiresAtUtc);
        Assert.Contains("User", result.Value.Roles);

        // Verify Identity user
        var user = await userManager.FindByEmailAsync("newuser@example.com");
        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user, "User"));

        // Verify UserProfile
        var profile = await context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
        Assert.NotNull(profile);
        Assert.Equal("Test User", profile.DisplayName);
        Assert.Equal("UTC", profile.TimeZoneId);
        Assert.False(profile.IsVerified);

        // Verify Wallet: 0 available, 0 held, NO ledger entries
        var wallet = await context.Wallets.FirstOrDefaultAsync(w => w.UserId == user.Id);
        Assert.NotNull(wallet);
        Assert.Equal(0, wallet.AvailableMinutes);
        Assert.Equal(0, wallet.HeldMinutes);

        var transactions = await context.CreditTransactions.Where(ct => ct.WalletId == user.Id).ToListAsync();
        Assert.Empty(transactions);

        // Verify UserSubscription: Active Free plan
        var subscription = await context.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == user.Id);
        Assert.NotNull(subscription);
        Assert.Equal(1, subscription.PlanId);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsFailure()
    {
        var (authService, _, _, _, _) = CreateTestHarness();
        var request = new RegisterRequestDto
        {
            Email = "duplicate@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Original User"
        };

        var first = await authService.RegisterAsync(request);
        Assert.True(first.IsSuccess);

        var duplicate = await authService.RegisterAsync(new RegisterRequestDto
        {
            Email = "duplicate@example.com",
            Password = "AnotherPassword123!",
            DisplayName = "Duplicate User"
        });

        Assert.True(duplicate.IsFailure);
        Assert.Contains("already exists", duplicate.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("short1!")]           // < 8 chars
    [InlineData("alllowercase123!")]  // no uppercase
    [InlineData("ALLUPPERCASE123!")]  // no lowercase
    [InlineData("NoDigitAtAll!")]     // no digit
    [InlineData("NoSpecialChar123")]  // no non-alphanumeric
    public async Task Register_WithWeakPassword_ReturnsFailure(string weakPassword)
    {
        var (authService, _, _, _, _) = CreateTestHarness();
        var request = new RegisterRequestDto
        {
            Email = "weakpass@example.com",
            Password = weakPassword,
            DisplayName = "Weak Password User"
        };

        var result = await authService.RegisterAsync(request);

        Assert.True(result.IsFailure);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task Register_WhenFreePlanMissing_FailsGracefully()
    {
        var (authService, context, _, _, _) = CreateTestHarness();
        // Remove Free plan to simulate missing catalog setup
        var plan = await context.SubscriptionPlans.FirstAsync(sp => sp.Code == "FREE");
        context.SubscriptionPlans.Remove(plan);
        await context.SaveChangesAsync();

        var request = new RegisterRequestDto
        {
            Email = "noplan@example.com",
            Password = "SecurePassword123!",
            DisplayName = "No Plan User"
        };

        var result = await authService.RegisterAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("FREE", result.Error);
    }

    [Fact]
    public async Task Register_IssuesJwt_WithSubEmailAndRoleClaims()
    {
        var (authService, _, _, _, _) = CreateTestHarness();
        var request = new RegisterRequestDto
        {
            Email = "claimstest@example.com",
            Password = "SecurePassword123!",
            DisplayName = "Claims User"
        };

        var result = await authService.RegisterAsync(request);
        Assert.True(result.IsSuccess);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Value.AccessToken);

        Assert.Equal("SkillSwap.Test", jwt.Issuer);
        Assert.Contains("SkillSwap.TestAudience", jwt.Audiences);

        var subClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        Assert.Equal(result.Value.UserId.ToString(), subClaim);

        var emailClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
        Assert.Equal("claimstest@example.com", emailClaim);

        var roleClaims = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Contains("User", roleClaims);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSuccessAndJwt()
    {
        var (authService, _, _, _, _) = CreateTestHarness();
        await authService.RegisterAsync(new RegisterRequestDto
        {
            Email = "logintest@example.com",
            Password = "CorrectPassword123!",
            DisplayName = "Login User"
        });

        var loginResult = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "logintest@example.com",
            Password = "CorrectPassword123!"
        });

        Assert.True(loginResult.IsSuccess);
        Assert.NotEmpty(loginResult.Value.AccessToken);
        Assert.Equal("logintest@example.com", loginResult.Value.Email);
        Assert.Contains("User", loginResult.Value.Roles);
    }

    [Fact]
    public async Task Login_WithNormalizedEmailVariation_Succeeds()
    {
        var (authService, _, _, _, _) = CreateTestHarness();
        await authService.RegisterAsync(new RegisterRequestDto
        {
            Email = "casetest@example.com",
            Password = "CorrectPassword123!",
            DisplayName = "Case User"
        });

        // Uppercase login email
        var loginResult = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "CASETEST@EXAMPLE.COM",
            Password = "CorrectPassword123!"
        });

        Assert.True(loginResult.IsSuccess);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_FailsWithoutRevealingDetails()
    {
        var (authService, _, _, _, _) = CreateTestHarness();
        await authService.RegisterAsync(new RegisterRequestDto
        {
            Email = "wrongpass@example.com",
            Password = "CorrectPassword123!",
            DisplayName = "Wrong Pass User"
        });

        var result = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "wrongpass@example.com",
            Password = "WrongPassword123!"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Invalid email or password.", result.Error);
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_ReturnsSameGenericMessage()
    {
        var (authService, _, _, _, _) = CreateTestHarness();

        var result = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "doesnotexist@example.com",
            Password = "AnyPassword123!"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Invalid email or password.", result.Error);
    }

    [Fact]
    public async Task Login_SoftDeletedAccount_ReturnsGenericError()
    {
        var (authService, context, userManager, _, _) = CreateTestHarness();
        await authService.RegisterAsync(new RegisterRequestDto
        {
            Email = "deleteduser@example.com",
            Password = "CorrectPassword123!",
            DisplayName = "Deleted User"
        });

        var user = await userManager.FindByEmailAsync("deleteduser@example.com");
        Assert.NotNull(user);
        user.IsDeleted = true;
        user.DeletedAtUtc = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        var result = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "deleteduser@example.com",
            Password = "CorrectPassword123!"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Invalid email or password.", result.Error);
    }

    [Fact]
    public async Task Login_FiveFailedAttempts_TriggersLockout()
    {
        var (authService, _, userManager, _, _) = CreateTestHarness();
        await authService.RegisterAsync(new RegisterRequestDto
        {
            Email = "lockouttest@example.com",
            Password = "CorrectPassword123!",
            DisplayName = "Lockout User"
        });

        // Fail 4 times
        for (int i = 1; i <= 4; i++)
        {
            var res = await authService.LoginAsync(new LoginRequestDto
            {
                Email = "lockouttest@example.com",
                Password = "BadPassword!"
            });
            Assert.True(res.IsFailure);
            Assert.Equal("Invalid email or password.", res.Error);
        }

        // 5th failed attempt triggers lockout
        var fifthResult = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "lockouttest@example.com",
            Password = "BadPassword!"
        });
        Assert.True(fifthResult.IsFailure);
        Assert.Contains("locked", fifthResult.Error, StringComparison.OrdinalIgnoreCase);

        var user = await userManager.FindByEmailAsync("lockouttest@example.com");
        Assert.NotNull(user);
        Assert.True(await userManager.IsLockedOutAsync(user));

        // 6th attempt with CORRECT password while locked out is still rejected
        var lockedWithCorrectPassword = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "lockouttest@example.com",
            Password = "CorrectPassword123!"
        });
        Assert.True(lockedWithCorrectPassword.IsFailure);
        Assert.Contains("locked", lockedWithCorrectPassword.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_SuccessfulAuth_ResetsFailedAccessCount()
    {
        var (authService, _, userManager, _, _) = CreateTestHarness();
        await authService.RegisterAsync(new RegisterRequestDto
        {
            Email = "resetcount@example.com",
            Password = "CorrectPassword123!",
            DisplayName = "Reset User"
        });

        // Fail 2 times
        for (int i = 0; i < 2; i++)
        {
            await authService.LoginAsync(new LoginRequestDto
            {
                Email = "resetcount@example.com",
                Password = "BadPassword!"
            });
        }

        var userBefore = await userManager.FindByEmailAsync("resetcount@example.com");
        Assert.Equal(2, await userManager.GetAccessFailedCountAsync(userBefore!));

        // Login with correct password
        var success = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "resetcount@example.com",
            Password = "CorrectPassword123!"
        });
        Assert.True(success.IsSuccess);

        var userAfter = await userManager.FindByEmailAsync("resetcount@example.com");
        Assert.Equal(0, await userManager.GetAccessFailedCountAsync(userAfter!));
    }
}
