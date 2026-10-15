using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Skills;

namespace SkillSwap.Tests.Unit;

public class CategoriesControllerTests
{
    private class FakeCategoryService : ICategoryService
    {
        public Func<Task<Result<IReadOnlyList<CategoryDto>>>>? OnGetActiveCategories { get; set; }
        public Func<int, Task<Result<CategoryDto>>>? OnGetCategoryById { get; set; }
        public Func<CreateCategoryRequest, Task<Result<CategoryDto>>>? OnCreateCategory { get; set; }

        public Task<Result<IReadOnlyList<CategoryDto>>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default)
        {
            if (OnGetActiveCategories != null)
                return OnGetActiveCategories();

            return Task.FromResult(Result.Success<IReadOnlyList<CategoryDto>>([
                new CategoryDto { Id = 1, Name = "Languages", IsActive = true, ActiveSkillsCount = 3 }
            ]));
        }

        public Task<Result<CategoryDto>> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            if (OnGetCategoryById != null)
                return OnGetCategoryById(id);

            return Task.FromResult(Result.Success(new CategoryDto { Id = id, Name = "Music", IsActive = true }));
        }

        public Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
        {
            if (OnCreateCategory != null)
                return OnCreateCategory(request);

            return Task.FromResult(Result.Success(new CategoryDto { Id = 10, Name = request.Name, IsActive = true }));
        }
    }

    [Fact]
    public async Task GetCategories_ReturnsOkWithList()
    {
        var fakeService = new FakeCategoryService();
        var controller = new CategoriesController(fakeService);

        var response = await controller.GetCategories(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        var categories = Assert.IsAssignableFrom<IReadOnlyList<CategoryDto>>(okResult.Value);
        Assert.Single(categories);
    }

    [Fact]
    public async Task GetCategoryById_WhenFound_ReturnsOk()
    {
        var fakeService = new FakeCategoryService();
        var controller = new CategoriesController(fakeService);

        var response = await controller.GetCategoryById(1, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        var cat = Assert.IsType<CategoryDto>(okResult.Value);
        Assert.Equal(1, cat.Id);
    }

    [Fact]
    public async Task GetCategoryById_WhenNotFound_ReturnsNotFound()
    {
        var fakeService = new FakeCategoryService
        {
            OnGetCategoryById = _ => Task.FromResult(Result.Failure<CategoryDto>("Category not found."))
        };
        var controller = new CategoriesController(fakeService);

        var response = await controller.GetCategoryById(999, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(response);
        Assert.Equal("Category not found.", notFound.Value);
    }

    [Fact]
    public async Task CreateCategory_ValidRequest_ReturnsCreatedAtAction()
    {
        var fakeService = new FakeCategoryService();
        var controller = new CategoriesController(fakeService);

        var request = new CreateCategoryRequest { Name = "Science" };
        var response = await controller.CreateCategory(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(response);
        var dto = Assert.IsType<CategoryDto>(created.Value);
        Assert.Equal("Science", dto.Name);
    }

    [Fact]
    public async Task CreateCategory_InvalidRequest_ReturnsBadRequest()
    {
        var fakeService = new FakeCategoryService
        {
            OnCreateCategory = _ => Task.FromResult(Result.Failure<CategoryDto>("Name is required."))
        };
        var controller = new CategoriesController(fakeService);

        var request = new CreateCategoryRequest { Name = "" };
        var response = await controller.CreateCategory(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal("Name is required.", badRequest.Value);
    }
}
