using SkillSwap.Application.DTOs.Skills;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class CategoryServiceTests
{
    private (CategoryService Service, ApplicationDbContext Context, TestDateTimeProvider Dtp) CreateTestHarness()
    {
        var context = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString("N"));
        var dtp = new TestDateTimeProvider { UtcNow = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc) };
        var service = new CategoryService(context, dtp);
        return (service, context, dtp);
    }

    [Fact]
    public async Task GetActiveCategories_ReturnsOnlyActiveCategories_WithSkillCounts()
    {
        var (service, context, _) = CreateTestHarness();

        var activeCat = new Category { Name = "Music", IsActive = true };
        var inactiveCat = new Category { Name = "Cooking", IsActive = false };
        context.Categories.AddRange(activeCat, inactiveCat);
        await context.SaveChangesAsync();

        context.Skills.AddRange(
            new Skill { CategoryId = activeCat.Id, Name = "Guitar", IsActive = true },
            new Skill { CategoryId = activeCat.Id, Name = "Piano", IsActive = false },
            new Skill { CategoryId = inactiveCat.Id, Name = "Baking", IsActive = true }
        );
        await context.SaveChangesAsync();

        var result = await service.GetActiveCategoriesAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        var dto = result.Value![0];
        Assert.Equal("Music", dto.Name);
        Assert.Equal(1, dto.ActiveSkillsCount);
    }

    [Fact]
    public async Task GetCategoryById_WhenActive_ReturnsCategoryDto()
    {
        var (service, context, _) = CreateTestHarness();

        var category = new Category { Name = "Languages", IsActive = true };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var result = await service.GetCategoryByIdAsync(category.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("Languages", result.Value!.Name);
    }

    [Fact]
    public async Task GetCategoryById_WhenInactiveOrMissing_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();

        var inactiveCategory = new Category { Name = "Drafts", IsActive = false };
        context.Categories.Add(inactiveCategory);
        await context.SaveChangesAsync();

        var resultInactive = await service.GetCategoryByIdAsync(inactiveCategory.Id);
        Assert.True(resultInactive.IsFailure);
        Assert.Contains("not found", resultInactive.Error, StringComparison.OrdinalIgnoreCase);

        var resultMissing = await service.GetCategoryByIdAsync(9999);
        Assert.True(resultMissing.IsFailure);
    }

    [Fact]
    public async Task CreateCategory_ValidRequest_CreatesAndReturnsDto()
    {
        var (service, _, _) = CreateTestHarness();

        var request = new CreateCategoryRequest { Name = "Programming" };
        var result = await service.CreateCategoryAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Programming", result.Value!.Name);
        Assert.True(result.Value.IsActive);
        Assert.Equal(0, result.Value.ActiveSkillsCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateCategory_EmptyOrWhitespaceName_ReturnsFailure(string name)
    {
        var (service, _, _) = CreateTestHarness();

        var request = new CreateCategoryRequest { Name = name };
        var result = await service.CreateCategoryAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateCategory_NameExceeds100Characters_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();

        var longName = new string('A', 101);
        var request = new CreateCategoryRequest { Name = longName };
        var result = await service.CreateCategoryAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("100 characters", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateCategory_DuplicateNameCaseInsensitive_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();

        context.Categories.Add(new Category { Name = "Science", IsActive = true });
        await context.SaveChangesAsync();

        var request = new CreateCategoryRequest { Name = "science" };
        var result = await service.CreateCategoryAsync(request);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
