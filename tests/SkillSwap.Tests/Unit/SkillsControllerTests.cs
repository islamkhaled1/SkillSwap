using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Skills;

namespace SkillSwap.Tests.Unit;

public class SkillsControllerTests
{
    private class FakeSkillService : ISkillService
    {
        public Func<int?, string?, Task<Result<IReadOnlyList<SkillDto>>>>? OnGetActiveSkills { get; set; }
        public Func<int, Task<Result<SkillDto>>>? OnGetSkillById { get; set; }
        public Func<CreateSkillRequest, Task<Result<SkillDto>>>? OnCreateSkill { get; set; }

        public Task<Result<IReadOnlyList<SkillDto>>> GetActiveSkillsAsync(int? categoryId = null, string? search = null, CancellationToken cancellationToken = default)
        {
            if (OnGetActiveSkills != null)
                return OnGetActiveSkills(categoryId, search);

            return Task.FromResult(Result.Success<IReadOnlyList<SkillDto>>([
                new SkillDto { Id = 1, CategoryId = 1, CategoryName = "Code", Name = "C#", IsActive = true }
            ]));
        }

        public Task<Result<SkillDto>> GetSkillByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            if (OnGetSkillById != null)
                return OnGetSkillById(id);

            return Task.FromResult(Result.Success(new SkillDto { Id = id, CategoryId = 1, Name = "C#", IsActive = true }));
        }

        public Task<Result<SkillDto>> CreateSkillAsync(CreateSkillRequest request, CancellationToken cancellationToken = default)
        {
            if (OnCreateSkill != null)
                return OnCreateSkill(request);

            return Task.FromResult(Result.Success(new SkillDto
            {
                Id = 100,
                CategoryId = request.CategoryId,
                Name = request.Name,
                IsActive = true
            }));
        }

        public Task<Result<IReadOnlyList<UserSkillDto>>> GetUserSkillsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<UserSkillDto>> AddUserSkillAsync(Guid userId, AddUserSkillRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<UserSkillDto>> UpdateUserSkillAsync(Guid userId, long userSkillId, UpdateUserSkillRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result> RemoveUserSkillAsync(Guid userId, long userSkillId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public async Task GetSkills_ReturnsOkWithList()
    {
        var fakeService = new FakeSkillService();
        var controller = new SkillsController(fakeService);

        var response = await controller.GetSkills(null, null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        var list = Assert.IsAssignableFrom<IReadOnlyList<SkillDto>>(okResult.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task GetSkillById_WhenFound_ReturnsOk()
    {
        var fakeService = new FakeSkillService();
        var controller = new SkillsController(fakeService);

        var response = await controller.GetSkillById(1, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        var skill = Assert.IsType<SkillDto>(okResult.Value);
        Assert.Equal(1, skill.Id);
    }

    [Fact]
    public async Task GetSkillById_WhenNotFound_ReturnsNotFound()
    {
        var fakeService = new FakeSkillService
        {
            OnGetSkillById = _ => Task.FromResult(Result.Failure<SkillDto>("Skill not found."))
        };
        var controller = new SkillsController(fakeService);

        var response = await controller.GetSkillById(999, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(response);
        Assert.Equal("Skill not found.", notFound.Value);
    }

    [Fact]
    public async Task CreateSkill_ValidRequest_ReturnsCreatedAtAction()
    {
        var fakeService = new FakeSkillService();
        var controller = new SkillsController(fakeService);

        var request = new CreateSkillRequest { CategoryId = 1, Name = "Rust" };
        var response = await controller.CreateSkill(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(response);
        var dto = Assert.IsType<SkillDto>(created.Value);
        Assert.Equal("Rust", dto.Name);
    }

    [Fact]
    public async Task CreateSkill_InvalidRequest_ReturnsBadRequest()
    {
        var fakeService = new FakeSkillService
        {
            OnCreateSkill = _ => Task.FromResult(Result.Failure<SkillDto>("Category not found."))
        };
        var controller = new SkillsController(fakeService);

        var request = new CreateSkillRequest { CategoryId = 999, Name = "Rust" };
        var response = await controller.CreateSkill(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal("Category not found.", badRequest.Value);
    }
}
