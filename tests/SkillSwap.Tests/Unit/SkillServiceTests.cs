using SkillSwap.Application.DTOs.Skills;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class SkillServiceTests
{
    private (SkillService Service, ApplicationDbContext Context, TestDateTimeProvider Dtp) CreateTestHarness()
    {
        var context = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString("N"));
        var dtp = new TestDateTimeProvider { UtcNow = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc) };
        var service = new SkillService(context, dtp);
        return (service, context, dtp);
    }

    [Fact]
    public async Task GetActiveSkills_FiltersByCategoryIdAndSearch()
    {
        var (service, context, _) = CreateTestHarness();

        var catProg = new Category { Name = "Programming", IsActive = true };
        var catLang = new Category { Name = "Languages", IsActive = true };
        context.Categories.AddRange(catProg, catLang);
        await context.SaveChangesAsync();

        context.Skills.AddRange(
            new Skill { CategoryId = catProg.Id, Name = "C# .NET", IsActive = true },
            new Skill { CategoryId = catProg.Id, Name = "Python", IsActive = true },
            new Skill { CategoryId = catLang.Id, Name = "Spanish", IsActive = true },
            new Skill { CategoryId = catProg.Id, Name = "Fortran", IsActive = false }
        );
        await context.SaveChangesAsync();

        var allResult = await service.GetActiveSkillsAsync();
        Assert.True(allResult.IsSuccess);
        Assert.Equal(3, allResult.Value!.Count);

        var progResult = await service.GetActiveSkillsAsync(categoryId: catProg.Id);
        Assert.True(progResult.IsSuccess);
        Assert.Equal(2, progResult.Value!.Count);

        var searchResult = await service.GetActiveSkillsAsync(search: "py");
        Assert.True(searchResult.IsSuccess);
        Assert.Single(searchResult.Value!);
        Assert.Equal("Python", searchResult.Value![0].Name);
    }

    [Fact]
    public async Task GetActiveSkills_ExcludesSkillsFromInactiveCategories()
    {
        var (service, context, _) = CreateTestHarness();

        var activeCat = new Category { Name = "Active Cat", IsActive = true };
        var inactiveCat = new Category { Name = "Inactive Cat", IsActive = false };
        context.Categories.AddRange(activeCat, inactiveCat);
        await context.SaveChangesAsync();

        context.Skills.AddRange(
            new Skill { CategoryId = activeCat.Id, Name = "Skill 1", IsActive = true },
            new Skill { CategoryId = inactiveCat.Id, Name = "Skill 2", IsActive = true }
        );
        await context.SaveChangesAsync();

        var result = await service.GetActiveSkillsAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("Skill 1", result.Value![0].Name);
    }

    [Fact]
    public async Task GetSkillById_WhenExistsAndActive_ReturnsSkillDto()
    {
        var (service, context, _) = CreateTestHarness();

        var cat = new Category { Name = "Music", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "Drums", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        var result = await service.GetSkillByIdAsync(skill.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("Drums", result.Value!.Name);
        Assert.Equal("Music", result.Value.CategoryName);
    }

    [Fact]
    public async Task GetSkillById_WhenNotFoundOrInactive_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();

        var cat = new Category { Name = "Art", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var inactiveSkill = new Skill { CategoryId = cat.Id, Name = "Sculpting", IsActive = false };
        context.Skills.Add(inactiveSkill);
        await context.SaveChangesAsync();

        var resultInactive = await service.GetSkillByIdAsync(inactiveSkill.Id);
        Assert.True(resultInactive.IsFailure);
        Assert.Contains("not found", resultInactive.Error, StringComparison.OrdinalIgnoreCase);

        var resultMissing = await service.GetSkillByIdAsync(9999);
        Assert.True(resultMissing.IsFailure);
    }

    [Fact]
    public async Task CreateSkill_ValidRequest_CreatesAndReturnsDto()
    {
        var (service, context, _) = CreateTestHarness();

        var cat = new Category { Name = "Design", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var request = new CreateSkillRequest { CategoryId = cat.Id, Name = "UI/UX Design" };
        var result = await service.CreateSkillAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("UI/UX Design", result.Value!.Name);
        Assert.Equal(cat.Id, result.Value.CategoryId);
        Assert.Equal("Design", result.Value.CategoryName);
    }

    [Fact]
    public async Task CreateSkill_WhenCategoryNotFoundOrInactive_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();

        var inactiveCat = new Category { Name = "Old", IsActive = false };
        context.Categories.Add(inactiveCat);
        await context.SaveChangesAsync();

        var requestMissing = new CreateSkillRequest { CategoryId = 9999, Name = "Test" };
        var resultMissing = await service.CreateSkillAsync(requestMissing);
        Assert.True(resultMissing.IsFailure);
        Assert.Contains("not found", resultMissing.Error, StringComparison.OrdinalIgnoreCase);

        var requestInactive = new CreateSkillRequest { CategoryId = inactiveCat.Id, Name = "Test" };
        var resultInactive = await service.CreateSkillAsync(requestInactive);
        Assert.True(resultInactive.IsFailure);
    }

    [Fact]
    public async Task CreateSkill_DuplicateInSameCategory_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();

        var cat = new Category { Name = "Sports", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        context.Skills.Add(new Skill { CategoryId = cat.Id, Name = "Tennis", IsActive = true });
        await context.SaveChangesAsync();

        var request = new CreateSkillRequest { CategoryId = cat.Id, Name = "tennis" };
        var result = await service.CreateSkillAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddUserSkill_ValidTeachAndLearn_AddsSuccessfully()
    {
        var (service, context, _) = CreateTestHarness();

        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Test User" });

        var cat = new Category { Name = "Coding", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill1 = new Skill { CategoryId = cat.Id, Name = "Rust", IsActive = true };
        var skill2 = new Skill { CategoryId = cat.Id, Name = "Go", IsActive = true };
        context.Skills.AddRange(skill1, skill2);
        await context.SaveChangesAsync();

        var addTeach = await service.AddUserSkillAsync(userId, new AddUserSkillRequest
        {
            SkillId = skill1.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Advanced
        });

        Assert.True(addTeach.IsSuccess);
        Assert.Equal("Rust", addTeach.Value!.SkillName);
        Assert.Equal(UserSkillType.Teach, addTeach.Value.Type);
        Assert.Equal(UserSkillLevel.Advanced, addTeach.Value.Level);

        var addLearn = await service.AddUserSkillAsync(userId, new AddUserSkillRequest
        {
            SkillId = skill2.Id,
            Type = UserSkillType.Learn,
            Level = UserSkillLevel.Beginner
        });

        Assert.True(addLearn.IsSuccess);
        Assert.Equal("Go", addLearn.Value!.SkillName);
        Assert.Equal(UserSkillType.Learn, addLearn.Value.Type);
    }

    [Fact]
    public async Task AddUserSkill_DuplicateSkillSameType_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();

        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Alice" });

        var cat = new Category { Name = "Languages", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "German", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        var first = await service.AddUserSkillAsync(userId, new AddUserSkillRequest
        {
            SkillId = skill.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Intermediate
        });
        Assert.True(first.IsSuccess);

        var duplicate = await service.AddUserSkillAsync(userId, new AddUserSkillRequest
        {
            SkillId = skill.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Advanced
        });
        Assert.True(duplicate.IsFailure);
        Assert.Contains("already declared", duplicate.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddUserSkill_WhenUserProfileNotFound_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();

        var cat = new Category { Name = "Music", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "Piano", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        var nonExistentUser = Guid.NewGuid();
        var result = await service.AddUserSkillAsync(nonExistentUser, new AddUserSkillRequest
        {
            SkillId = skill.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Beginner
        });

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateUserSkill_ValidOwner_UpdatesLevelSuccessfully()
    {
        var (service, context, _) = CreateTestHarness();

        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Bob" });

        var cat = new Category { Name = "Music", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "Violin", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        var userSkill = new UserSkill
        {
            UserId = userId,
            SkillId = skill.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Beginner
        };
        context.UserSkills.Add(userSkill);
        await context.SaveChangesAsync();

        var updateResult = await service.UpdateUserSkillAsync(userId, userSkill.Id, new UpdateUserSkillRequest
        {
            Level = UserSkillLevel.Advanced
        });

        Assert.True(updateResult.IsSuccess);
        Assert.Equal(UserSkillLevel.Advanced, updateResult.Value!.Level);
    }

    [Fact]
    public async Task UpdateUserSkill_DifferentUser_ReturnsUnauthorizedFailure()
    {
        var (service, context, _) = CreateTestHarness();

        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        var cat = new Category { Name = "Music", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "Violin", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        var userSkill = new UserSkill
        {
            UserId = ownerId,
            SkillId = skill.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Beginner
        };
        context.UserSkills.Add(userSkill);
        await context.SaveChangesAsync();

        var updateResult = await service.UpdateUserSkillAsync(attackerId, userSkill.Id, new UpdateUserSkillRequest
        {
            Level = UserSkillLevel.Advanced
        });

        Assert.True(updateResult.IsFailure);
        Assert.Contains("not authorized", updateResult.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveUserSkill_ValidOwner_DeletesSuccessfully()
    {
        var (service, context, _) = CreateTestHarness();

        var userId = Guid.NewGuid();
        var cat = new Category { Name = "Art", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "Painting", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        var userSkill = new UserSkill
        {
            UserId = userId,
            SkillId = skill.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Intermediate
        };
        context.UserSkills.Add(userSkill);
        await context.SaveChangesAsync();

        var deleteResult = await service.RemoveUserSkillAsync(userId, userSkill.Id);
        Assert.True(deleteResult.IsSuccess);

        var listResult = await service.GetUserSkillsAsync(userId);
        Assert.True(listResult.IsSuccess);
        Assert.Empty(listResult.Value!);
    }

    [Fact]
    public async Task RemoveUserSkill_DifferentUser_ReturnsUnauthorizedFailure()
    {
        var (service, context, _) = CreateTestHarness();

        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        var cat = new Category { Name = "Art", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "Painting", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        var userSkill = new UserSkill
        {
            UserId = ownerId,
            SkillId = skill.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Intermediate
        };
        context.UserSkills.Add(userSkill);
        await context.SaveChangesAsync();

        var deleteResult = await service.RemoveUserSkillAsync(attackerId, userSkill.Id);
        Assert.True(deleteResult.IsFailure);
        Assert.Contains("not authorized", deleteResult.Error, StringComparison.OrdinalIgnoreCase);
    }
}
