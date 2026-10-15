using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Profiles;

namespace SkillSwap.Tests.Unit;

public class ProfilesControllerTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public string? Email { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }

    private class FakeProfileService : IProfileService
    {
        public Func<Guid, Task<Result<UserProfileDto>>>? OnGetMyProfile { get; set; }
        public Func<Guid, Task<Result<UserProfileDto>>>? OnGetProfileById { get; set; }
        public Func<Guid, UpdateProfileRequestDto, Task<Result<UserProfileDto>>>? OnUpdateMyProfile { get; set; }

        public Task<Result<UserProfileDto>> GetMyProfileAsync(Guid currentUserId, CancellationToken cancellationToken = default)
        {
            if (OnGetMyProfile != null)
                return OnGetMyProfile(currentUserId);

            return Task.FromResult(Result.Success(new UserProfileDto
            {
                UserId = currentUserId,
                DisplayName = "Default User",
                TimeZoneId = "UTC"
            }));
        }

        public Task<Result<UserProfileDto>> GetProfileByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (OnGetProfileById != null)
                return OnGetProfileById(userId);

            return Task.FromResult(Result.Success(new UserProfileDto
            {
                UserId = userId,
                DisplayName = "Public User",
                TimeZoneId = "UTC"
            }));
        }

        public Task<Result<UserProfileDto>> UpdateMyProfileAsync(Guid currentUserId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default)
        {
            if (OnUpdateMyProfile != null)
                return OnUpdateMyProfile(currentUserId, request);

            return Task.FromResult(Result.Success(new UserProfileDto
            {
                UserId = currentUserId,
                DisplayName = request.DisplayName,
                Bio = request.Bio,
                PhotoUrl = request.PhotoUrl,
                TimeZoneId = request.TimeZoneId
            }));
        }
    }

    [Fact]
    public async Task GetMyProfile_WhenAuthenticated_Returns200OK_WithProfile()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var fakeProfileService = new FakeProfileService();
        var controller = new ProfilesController(fakeProfileService, fakeCurrentUserService);

        var response = await controller.GetMyProfile(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var dto = Assert.IsType<UserProfileDto>(okResult.Value);
        Assert.Equal(currentUserId, dto.UserId);
    }

    [Fact]
    public async Task GetMyProfile_WhenUnauthenticated_Returns401Unauthorized()
    {
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = null };
        var fakeProfileService = new FakeProfileService();
        var controller = new ProfilesController(fakeProfileService, fakeCurrentUserService);

        var response = await controller.GetMyProfile(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
    }

    [Fact]
    public async Task GetMyProfile_WhenNotFound_Returns404NotFound()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var fakeProfileService = new FakeProfileService
        {
            OnGetMyProfile = id => Task.FromResult(Result.Failure<UserProfileDto>($"Profile for user '{id}' was not found."))
        };
        var controller = new ProfilesController(fakeProfileService, fakeCurrentUserService);

        var response = await controller.GetMyProfile(CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(response);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task UpdateMyProfile_WhenValid_Returns200OK_WithUpdatedProfile()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var fakeProfileService = new FakeProfileService();
        var controller = new ProfilesController(fakeProfileService, fakeCurrentUserService);

        var request = new UpdateProfileRequestDto
        {
            DisplayName = "Updated Name",
            Bio = "Updated Bio",
            TimeZoneId = "UTC"
        };

        var response = await controller.UpdateMyProfile(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var dto = Assert.IsType<UserProfileDto>(okResult.Value);
        Assert.Equal("Updated Name", dto.DisplayName);
    }

    [Fact]
    public async Task UpdateMyProfile_WhenNotFound_Returns404NotFound()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var fakeProfileService = new FakeProfileService
        {
            OnUpdateMyProfile = (id, _) => Task.FromResult(Result.Failure<UserProfileDto>($"Profile for user '{id}' was not found."))
        };
        var controller = new ProfilesController(fakeProfileService, fakeCurrentUserService);

        var request = new UpdateProfileRequestDto
        {
            DisplayName = "Updated Name",
            TimeZoneId = "UTC"
        };

        var response = await controller.UpdateMyProfile(request, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(response);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task UpdateMyProfile_WhenInvalidInput_Returns400BadRequest()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var fakeProfileService = new FakeProfileService
        {
            OnUpdateMyProfile = (_, _) => Task.FromResult(Result.Failure<UserProfileDto>("PhotoUrl must be a valid absolute HTTP or HTTPS URL."))
        };
        var controller = new ProfilesController(fakeProfileService, fakeCurrentUserService);

        var request = new UpdateProfileRequestDto
        {
            DisplayName = "Updated Name",
            PhotoUrl = "invalid-url",
            TimeZoneId = "UTC"
        };

        var response = await controller.UpdateMyProfile(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
        Assert.Equal("PhotoUrl must be a valid absolute HTTP or HTTPS URL.", badRequestResult.Value);
    }

    [Fact]
    public async Task GetProfileById_WhenExists_Returns200OK_WithProfile()
    {
        var targetId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = Guid.NewGuid() };
        var fakeProfileService = new FakeProfileService();
        var controller = new ProfilesController(fakeProfileService, fakeCurrentUserService);

        var response = await controller.GetProfileById(targetId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var dto = Assert.IsType<UserProfileDto>(okResult.Value);
        Assert.Equal(targetId, dto.UserId);
    }

    [Fact]
    public async Task GetProfileById_WhenNotFound_Returns404NotFound()
    {
        var targetId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = Guid.NewGuid() };
        var fakeProfileService = new FakeProfileService
        {
            OnGetProfileById = id => Task.FromResult(Result.Failure<UserProfileDto>($"Profile for user '{id}' was not found."))
        };
        var controller = new ProfilesController(fakeProfileService, fakeCurrentUserService);

        var response = await controller.GetProfileById(targetId, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(response);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
    }
}
