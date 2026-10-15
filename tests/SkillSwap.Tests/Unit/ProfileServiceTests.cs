using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.DTOs.Profiles;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class ProfileServiceTests
{
    private (ProfileService Service, ApplicationDbContext Context, TestDateTimeProvider Dtp) CreateTestHarness(string? dbName = null)
    {
        var context = TestDbContextFactory.CreateInMemoryContext(dbName ?? Guid.NewGuid().ToString("N"));
        var dtp = new TestDateTimeProvider { UtcNow = new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Utc) };
        var service = new ProfileService(context, dtp);
        return (service, context, dtp);
    }

    [Fact]
    public async Task GetMyProfile_WhenExists_ReturnsProfileDto()
    {
        var (service, context, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var profile = new UserProfile
        {
            UserId = userId,
            DisplayName = "Jane Doe",
            Bio = "Software developer",
            PhotoUrl = "https://images.example.com/avatar1.png",
            TimeZoneId = "UTC",
            IsVerified = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
            UpdatedAtUtc = null
        };
        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();

        var result = await service.GetMyProfileAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(userId, result.Value.UserId);
        Assert.Equal("Jane Doe", result.Value.DisplayName);
        Assert.Equal("Software developer", result.Value.Bio);
        Assert.Equal("https://images.example.com/avatar1.png", result.Value.PhotoUrl);
        Assert.Equal("UTC", result.Value.TimeZoneId);
        Assert.True(result.Value.IsVerified);
    }

    [Fact]
    public async Task GetMyProfile_WhenNotFound_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();
        var nonExistentId = Guid.NewGuid();

        var result = await service.GetMyProfileAsync(nonExistentId);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetProfileById_WhenExists_ReturnsProfileDto_WithoutPrivateData()
    {
        var (service, context, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var profile = new UserProfile
        {
            UserId = userId,
            DisplayName = "Public Teacher",
            Bio = "Teaches C# and Docker",
            PhotoUrl = "https://example.com/photos/teacher.jpg",
            TimeZoneId = "Eastern Standard Time",
            IsVerified = false,
            CreatedAtUtc = DateTime.UtcNow.AddMonths(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-2)
        };
        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();

        var result = await service.GetProfileByIdAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value.UserId);
        Assert.Equal("Public Teacher", result.Value.DisplayName);
        Assert.Equal("Teaches C# and Docker", result.Value.Bio);
        Assert.Equal("https://example.com/photos/teacher.jpg", result.Value.PhotoUrl);
        Assert.Equal("Eastern Standard Time", result.Value.TimeZoneId);
        Assert.False(result.Value.IsVerified);
    }

    [Fact]
    public async Task GetProfileById_WhenNotFound_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();
        var nonExistentId = Guid.NewGuid();

        var result = await service.GetProfileByIdAsync(nonExistentId);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateMyProfile_WithValidData_UpdatesAllowedFields_AndSetsUpdatedAtUtc()
    {
        var (service, context, dtp) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var profile = new UserProfile
        {
            UserId = userId,
            DisplayName = "Old Name",
            Bio = "Old Bio",
            PhotoUrl = null,
            TimeZoneId = "UTC",
            IsVerified = true, // Should NOT be changed by user update
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = null
        };
        context.UserProfiles.Add(profile);
        await context.SaveChangesAsync();

        var request = new UpdateProfileRequestDto
        {
            DisplayName = "New Name",
            Bio = "Updated modern bio",
            PhotoUrl = "https://cdn.example.com/avatar.jpg",
            TimeZoneId = "UTC"
        };

        var result = await service.UpdateMyProfileAsync(userId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Value.DisplayName);
        Assert.Equal("Updated modern bio", result.Value.Bio);
        Assert.Equal("https://cdn.example.com/avatar.jpg", result.Value.PhotoUrl);
        Assert.Equal("UTC", result.Value.TimeZoneId);
        Assert.True(result.Value.IsVerified); // Preserved
        Assert.Equal(dtp.UtcNow, result.Value.UpdatedAtUtc);

        // Verify database persistence
        var dbProfile = await context.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == userId);
        Assert.Equal("New Name", dbProfile.DisplayName);
        Assert.Equal("Updated modern bio", dbProfile.Bio);
        Assert.Equal("https://cdn.example.com/avatar.jpg", dbProfile.PhotoUrl);
        Assert.Equal(dtp.UtcNow, dbProfile.UpdatedAtUtc);
        Assert.True(dbProfile.IsVerified);
    }

    [Fact]
    public async Task UpdateMyProfile_PreservesIsVerified_RegardlessOfInput()
    {
        var (service, context, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var unverifiedProfile = new UserProfile
        {
            UserId = userId,
            DisplayName = "Unverified User",
            IsVerified = false
        };
        context.UserProfiles.Add(unverifiedProfile);
        await context.SaveChangesAsync();

        var request = new UpdateProfileRequestDto
        {
            DisplayName = "Attempted Update",
            TimeZoneId = "UTC"
        };

        var result = await service.UpdateMyProfileAsync(userId, request);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsVerified);

        var dbProfile = await context.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == userId);
        Assert.False(dbProfile.IsVerified);
    }

    [Fact]
    public async Task UpdateMyProfile_DoesNotMutateAnotherUsersProfile()
    {
        var (service, context, _) = CreateTestHarness();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var profile1 = new UserProfile { UserId = user1Id, DisplayName = "User One", TimeZoneId = "UTC" };
        var profile2 = new UserProfile { UserId = user2Id, DisplayName = "User Two", TimeZoneId = "UTC" };
        context.UserProfiles.AddRange(profile1, profile2);
        await context.SaveChangesAsync();

        // User 1 updates their profile
        var request = new UpdateProfileRequestDto
        {
            DisplayName = "User One Updated",
            TimeZoneId = "UTC"
        };

        var result = await service.UpdateMyProfileAsync(user1Id, request);
        Assert.True(result.IsSuccess);

        // User 2 must remain untouched
        var dbProfile2 = await context.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == user2Id);
        Assert.Equal("User Two", dbProfile2.DisplayName);
    }

    [Fact]
    public async Task UpdateMyProfile_DoesNotMutateWalletOrFinancialRecords()
    {
        var (service, context, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        var profile = new UserProfile { UserId = userId, DisplayName = "Wallet User", TimeZoneId = "UTC" };
        var wallet = new Wallet { UserId = userId, AvailableMinutes = 180, HeldMinutes = 60 };
        context.UserProfiles.Add(profile);
        context.Wallets.Add(wallet);
        await context.SaveChangesAsync();

        var request = new UpdateProfileRequestDto
        {
            DisplayName = "Wallet User New Name",
            Bio = "New Bio",
            TimeZoneId = "UTC"
        };

        var result = await service.UpdateMyProfileAsync(userId, request);
        Assert.True(result.IsSuccess);

        // Verify wallet is completely unchanged
        var dbWallet = await context.Wallets.AsNoTracking().FirstAsync(w => w.UserId == userId);
        Assert.Equal(180, dbWallet.AvailableMinutes);
        Assert.Equal(60, dbWallet.HeldMinutes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateMyProfile_EmptyDisplayName_ReturnsFailure(string emptyDisplayName)
    {
        var (service, context, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Original" });
        await context.SaveChangesAsync();

        var result = await service.UpdateMyProfileAsync(userId, new UpdateProfileRequestDto
        {
            DisplayName = emptyDisplayName,
            TimeZoneId = "UTC"
        });

        Assert.True(result.IsFailure);
        Assert.Contains("DisplayName", result.Error);
    }

    [Fact]
    public async Task UpdateMyProfile_DisplayNameExceeding100Chars_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Original" });
        await context.SaveChangesAsync();

        var result = await service.UpdateMyProfileAsync(userId, new UpdateProfileRequestDto
        {
            DisplayName = new string('A', 101),
            TimeZoneId = "UTC"
        });

        Assert.True(result.IsFailure);
        Assert.Contains("100 characters", result.Error);
    }

    [Fact]
    public async Task UpdateMyProfile_BioExceeding1000Chars_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Original" });
        await context.SaveChangesAsync();

        var result = await service.UpdateMyProfileAsync(userId, new UpdateProfileRequestDto
        {
            DisplayName = "Valid Name",
            Bio = new string('B', 1001),
            TimeZoneId = "UTC"
        });

        Assert.True(result.IsFailure);
        Assert.Contains("1000 characters", result.Error);
    }

    [Theory]
    [InlineData("not-a-valid-url")]
    [InlineData("ftp://example.com/avatar.png")]
    [InlineData("file:///etc/passwd")]
    [InlineData("/relative/path/avatar.png")]
    public async Task UpdateMyProfile_InvalidPhotoUrl_ReturnsFailure(string invalidUrl)
    {
        var (service, context, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Original" });
        await context.SaveChangesAsync();

        var result = await service.UpdateMyProfileAsync(userId, new UpdateProfileRequestDto
        {
            DisplayName = "Valid Name",
            PhotoUrl = invalidUrl,
            TimeZoneId = "UTC"
        });

        Assert.True(result.IsFailure);
        Assert.Contains("absolute HTTP or HTTPS URL", result.Error);
    }

    [Theory]
    [InlineData("Invalid/TimeZone_Zone")]
    [InlineData("NonExistentTimezone12345")]
    [InlineData("Mars/Olympus_Mons")]
    public async Task UpdateMyProfile_InvalidTimeZone_ReturnsFailure(string invalidTimeZone)
    {
        var (service, context, _) = CreateTestHarness();
        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Original" });
        await context.SaveChangesAsync();

        var result = await service.UpdateMyProfileAsync(userId, new UpdateProfileRequestDto
        {
            DisplayName = "Valid Name",
            TimeZoneId = invalidTimeZone
        });

        Assert.True(result.IsFailure);
        Assert.Contains("valid system time zone", result.Error);
    }

    [Fact]
    public async Task UpdateMyProfile_MissingProfile_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();
        var nonExistentId = Guid.NewGuid();

        var result = await service.UpdateMyProfileAsync(nonExistentId, new UpdateProfileRequestDto
        {
            DisplayName = "New Name",
            TimeZoneId = "UTC"
        });

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error);
    }
}
