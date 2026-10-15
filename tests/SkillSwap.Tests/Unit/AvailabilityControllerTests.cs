using Microsoft.AspNetCore.Mvc;
using SkillSwap.API.Controllers;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Availability;

namespace SkillSwap.Tests.Unit;

public class AvailabilityControllerTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public string? Email { get; set; }
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }

    private class FakeAvailabilityService : IAvailabilityService
    {
        public Func<Guid, Task<Result<IReadOnlyList<AvailabilitySlotDto>>>>? OnGetUserAvailability { get; set; }
        public Func<Guid, UpdateAvailabilityRequest, Task<Result<IReadOnlyList<AvailabilitySlotDto>>>>? OnUpdateUserAvailability { get; set; }

        public Task<Result<IReadOnlyList<AvailabilitySlotDto>>> GetUserAvailabilityAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (OnGetUserAvailability != null)
                return OnGetUserAvailability(userId);

            return Task.FromResult(Result.Success<IReadOnlyList<AvailabilitySlotDto>>([
                new AvailabilitySlotDto
                {
                    Id = 1,
                    UserId = userId,
                    DayOfWeek = DayOfWeek.Monday,
                    StartTime = new TimeOnly(9, 0),
                    EndTime = new TimeOnly(11, 0)
                }
            ]));
        }

        public Task<Result<IReadOnlyList<AvailabilitySlotDto>>> UpdateUserAvailabilityAsync(Guid userId, UpdateAvailabilityRequest request, CancellationToken cancellationToken = default)
        {
            if (OnUpdateUserAvailability != null)
                return OnUpdateUserAvailability(userId, request);

            return Task.FromResult(Result.Success<IReadOnlyList<AvailabilitySlotDto>>([
                new AvailabilitySlotDto
                {
                    Id = 2,
                    UserId = userId,
                    DayOfWeek = DayOfWeek.Tuesday,
                    StartTime = new TimeOnly(14, 0),
                    EndTime = new TimeOnly(16, 0)
                }
            ]));
        }
    }

    [Fact]
    public async Task GetMyAvailability_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = null };
        var controller = new AvailabilityController(new FakeAvailabilityService(), fakeCurrentUserService);

        var response = await controller.GetMyAvailability(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
    }

    [Fact]
    public async Task GetMyAvailability_WhenAuthenticated_ReturnsOkWithList()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var controller = new AvailabilityController(new FakeAvailabilityService(), fakeCurrentUserService);

        var response = await controller.GetMyAvailability(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        var slots = Assert.IsAssignableFrom<IReadOnlyList<AvailabilitySlotDto>>(okResult.Value);
        Assert.Single(slots);
        Assert.Equal(currentUserId, slots[0].UserId);
    }

    [Fact]
    public async Task UpdateMyAvailability_ValidRequest_ReturnsOkWithUpdatedSlots()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var controller = new AvailabilityController(new FakeAvailabilityService(), fakeCurrentUserService);

        var request = new UpdateAvailabilityRequest
        {
            Slots = [new AvailabilitySlotRequest
            {
                DayOfWeek = DayOfWeek.Tuesday,
                StartTime = new TimeOnly(14, 0),
                EndTime = new TimeOnly(16, 0)
            }]
        };

        var response = await controller.UpdateMyAvailability(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        var slots = Assert.IsAssignableFrom<IReadOnlyList<AvailabilitySlotDto>>(okResult.Value);
        Assert.Single(slots);
    }

    [Fact]
    public async Task UpdateMyAvailability_WhenInvalidTimes_ReturnsBadRequest()
    {
        var currentUserId = Guid.NewGuid();
        var fakeCurrentUserService = new FakeCurrentUserService { UserId = currentUserId };
        var fakeService = new FakeAvailabilityService
        {
            OnUpdateUserAvailability = (_, _) => Task.FromResult(Result.Failure<IReadOnlyList<AvailabilitySlotDto>>("StartTime must be strictly earlier than EndTime."))
        };
        var controller = new AvailabilityController(fakeService, fakeCurrentUserService);

        var request = new UpdateAvailabilityRequest();
        var response = await controller.UpdateMyAvailability(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal("StartTime must be strictly earlier than EndTime.", badRequest.Value);
    }
}
