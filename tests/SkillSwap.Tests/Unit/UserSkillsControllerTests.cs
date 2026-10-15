using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Skills;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Tests.Unit;

public class UserSkillsControllerTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public string? Email { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }

    private class FakeSkillService : ISkillService
    {
        public Func<Guid, Task<Result<IReadOnlyList<UserSkillDto>>>>? OnGetUserSkills { get; set; }
        public Func<Guid, AddUserSkillRequest, Task<Result<UserSkillDto>>>? OnAddUserSkill { get; set; }
        public Func<Guid, long, UpdateUserSkillRequest, Task<Result<UserSkillDto>>>? OnUpdateUserSkill { get; set; }
        public Func<Guid, long, Task<Result>>? OnRemoveUserSkill { get; set; }

        public Task<Result<IReadOnlyList<SkillDto>>> GetActiveSkillsAsync(int? categoryId = null, string? search = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<SkillDto>> GetSkillByIdAsync(int id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<SkillDto>> CreateSkillAsync(CreateSkillRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<IReadOnlyList<UserSkillDto>>> GetUserSkillsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (OnGetUserSkills != null)
                return OnGetUserSkills(userId);

            return Task.FromResult(Result.Success<IReadOnlyList<UserSkillDto>>([
                new UserSkillDto { Id = 1, UserId = userId, SkillId = 1, SkillName = "C#", Type = UserSkillType.Teach, Level = UserSkillLevel.Advanced }
            ]));
        }

        public Task<Result<UserSkillDto>> AddUserSkillAsync(Guid userId, AddUserSkillRequest request, CancellationToken cancellationToken = default)
        {
            if (OnAddUserSkill != null)
                return OnAddUserSkill(userId, request);

            return Task.FromResult(Result.Success(new UserSkillDto
            {
                Id = 10,
                UserId = userId,
                SkillId = request.SkillId,
                SkillName = "C#",
                Type = request.Type,
                Level = request.Level
            }));
        }

        public Task<Result<UserSkillDto>> UpdateUserSkillAsync(Guid userId, long userSkillId, UpdateUserSkillRequest request, CancellationToken cancellationToken = default)
        {
            if (OnUpdateUserSkill != null)
                return OnUpdateUserSkill(userId, userSkillId, request);

            return Task.FromResult(Result.Success(new UserSkillDto
            {
                Id = userSkillId,
                UserId = userId,
                SkillId = 1,
                SkillName = "C#",
                Type = UserSkillType.Teach,
                Level = request.Level
            }));
        }

        public Task<Result> RemoveUserSkillAsync(Guid userId, long userSkillId, CancellationToken cancellationToken = default)
        {
            if (OnRemoveUserSkill != null)
                return OnRemoveUserSkill(userId, userSkillId);

            return Task.FromResult(Result.Success());
        }
    }

    [Fact]
    public async Task GetMySkills_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = null };
        var controller = new UserSkillsController(new FakeSkillService(), fakeCurrentUserService);

        var response = await controller.GetMySkills(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
    }

    [Fact]
    public async Task GetMySkills_WhenAuthenticated_ReturnsOkWithList()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var controller = new UserSkillsController(new FakeSkillService(), fakeCurrentUserService);

        var response = await controller.GetMySkills(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        var skills = Assert.IsAssignableFrom<IReadOnlyList<UserSkillDto>>(okResult.Value);
        Assert.Single(skills);
        Assert.Equal(currentUserId, skills[0].UserId);
    }

    [Fact]
    public async Task AddUserSkill_ValidRequest_ReturnsCreatedAtAction()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var controller = new UserSkillsController(new FakeSkillService(), fakeCurrentUserService);

        var request = new AddUserSkillRequest
        {
            SkillId = 1,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Intermediate
        };

        var response = await controller.AddUserSkill(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(response);
        var dto = Assert.IsType<UserSkillDto>(created.Value);
        Assert.Equal(currentUserId, dto.UserId);
        Assert.Equal(UserSkillLevel.Intermediate, dto.Level);
    }

    [Fact]
    public async Task AddUserSkill_WhenNotFound_ReturnsNotFound()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var fakeService = new FakeSkillService
        {
            OnAddUserSkill = (_, _) => Task.FromResult(Result.Failure<UserSkillDto>("Skill not found."))
        };
        var controller = new UserSkillsController(fakeService, fakeCurrentUserService);

        var request = new AddUserSkillRequest { SkillId = 999, Type = UserSkillType.Teach, Level = UserSkillLevel.Beginner };
        var response = await controller.AddUserSkill(request, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(response);
        Assert.Equal("Skill not found.", notFound.Value);
    }

    [Fact]
    public async Task UpdateUserSkill_WhenUnauthorized_ReturnsForbid()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var fakeService = new FakeSkillService
        {
            OnUpdateUserSkill = (_, _, _) => Task.FromResult(Result.Failure<UserSkillDto>("You are not authorized to update another user's skill."))
        };
        var controller = new UserSkillsController(fakeService, fakeCurrentUserService);

        var request = new UpdateUserSkillRequest { Level = UserSkillLevel.Advanced };
        var response = await controller.UpdateUserSkill(5, request, CancellationToken.None);

        Assert.IsType<ForbidResult>(response);
    }

    [Fact]
    public async Task RemoveUserSkill_WhenSuccess_ReturnsNoContent()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var controller = new UserSkillsController(new FakeSkillService(), fakeCurrentUserService);

        var response = await controller.RemoveUserSkill(5, CancellationToken.None);

        Assert.IsType<NoContentResult>(response);
    }
}
