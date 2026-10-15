using SkillSwap.Application.DTOs.Availability;
using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class AvailabilityServiceTests
{
    private (AvailabilityService Service, ApplicationDbContext Context, TestDateTimeProvider Dtp) CreateTestHarness()
    {
        var context = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString("N"));
        var dtp = new TestDateTimeProvider { UtcNow = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc) };
        var service = new AvailabilityService(context, dtp);
        return (service, context, dtp);
    }

    [Fact]
    public async Task GetUserAvailability_ReturnsSlotsOrderedByDayAndStartTime()
    {
        var (service, context, _) = CreateTestHarness();

        var userId = Guid.NewGuid();
        context.AvailabilitySlots.AddRange(
            new AvailabilitySlot
            {
                UserId = userId,
                DayOfWeek = DayOfWeek.Wednesday,
                StartTime = new TimeOnly(14, 0),
                EndTime = new TimeOnly(16, 0)
            },
            new AvailabilitySlot
            {
                UserId = userId,
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeOnly(18, 0),
                EndTime = new TimeOnly(20, 0)
            },
            new AvailabilitySlot
            {
                UserId = userId,
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(11, 0)
            }
        );
        await context.SaveChangesAsync();

        var result = await service.GetUserAvailabilityAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Count);
        Assert.Equal(DayOfWeek.Monday, result.Value[0].DayOfWeek);
        Assert.Equal(new TimeOnly(9, 0), result.Value[0].StartTime);
        Assert.Equal(DayOfWeek.Monday, result.Value[1].DayOfWeek);
        Assert.Equal(new TimeOnly(18, 0), result.Value[1].StartTime);
        Assert.Equal(DayOfWeek.Wednesday, result.Value[2].DayOfWeek);
    }

    [Fact]
    public async Task UpdateUserAvailability_ValidSlots_ReplacesExistingSlotsAtomically()
    {
        var (service, context, _) = CreateTestHarness();

        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Charlie" });
        context.AvailabilitySlots.Add(new AvailabilitySlot
        {
            UserId = userId,
            DayOfWeek = DayOfWeek.Friday,
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(12, 0)
        });
        await context.SaveChangesAsync();

        var request = new UpdateAvailabilityRequest
        {
            Slots =
            [
                new AvailabilitySlotRequest
                {
                    DayOfWeek = DayOfWeek.Tuesday,
                    StartTime = new TimeOnly(9, 0),
                    EndTime = new TimeOnly(12, 0)
                },
                new AvailabilitySlotRequest
                {
                    DayOfWeek = DayOfWeek.Thursday,
                    StartTime = new TimeOnly(14, 0),
                    EndTime = new TimeOnly(17, 0)
                }
            ]
        };

        var result = await service.UpdateUserAvailabilityAsync(userId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Contains(result.Value, s => s.DayOfWeek == DayOfWeek.Tuesday);
        Assert.Contains(result.Value, s => s.DayOfWeek == DayOfWeek.Thursday);
        Assert.DoesNotContain(result.Value, s => s.DayOfWeek == DayOfWeek.Friday);
    }

    [Fact]
    public async Task UpdateUserAvailability_EmptySlots_ClearsAllSlots()
    {
        var (service, context, _) = CreateTestHarness();

        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Charlie" });
        context.AvailabilitySlots.Add(new AvailabilitySlot
        {
            UserId = userId,
            DayOfWeek = DayOfWeek.Sunday,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(10, 0)
        });
        await context.SaveChangesAsync();

        var request = new UpdateAvailabilityRequest { Slots = [] };
        var result = await service.UpdateUserAvailabilityAsync(userId, request);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task UpdateUserAvailability_WhenUserNotFound_ReturnsFailure()
    {
        var (service, _, _) = CreateTestHarness();

        var nonExistentUser = Guid.NewGuid();
        var request = new UpdateAvailabilityRequest
        {
            Slots = [new AvailabilitySlotRequest
            {
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(12, 0)
            }]
        };

        var result = await service.UpdateUserAvailabilityAsync(nonExistentUser, request);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateUserAvailability_StartTimeEqualsEndTime_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();

        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Charlie" });
        await context.SaveChangesAsync();

        var request = new UpdateAvailabilityRequest
        {
            Slots = [new AvailabilitySlotRequest
            {
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(10, 0)
            }]
        };

        var result = await service.UpdateUserAvailabilityAsync(userId, request);

        Assert.True(result.IsFailure);
        Assert.Contains("must be strictly earlier", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateUserAvailability_StartTimeGreaterThanEndTime_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();

        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Charlie" });
        await context.SaveChangesAsync();

        var request = new UpdateAvailabilityRequest
        {
            Slots = [new AvailabilitySlotRequest
            {
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeOnly(15, 0),
                EndTime = new TimeOnly(14, 0)
            }]
        };

        var result = await service.UpdateUserAvailabilityAsync(userId, request);

        Assert.True(result.IsFailure);
        Assert.Contains("must be strictly earlier", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateUserAvailability_OverlappingSlotsSameDay_ReturnsFailure()
    {
        var (service, context, _) = CreateTestHarness();

        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Charlie" });
        await context.SaveChangesAsync();

        var request = new UpdateAvailabilityRequest
        {
            Slots =
            [
                new AvailabilitySlotRequest
                {
                    DayOfWeek = DayOfWeek.Tuesday,
                    StartTime = new TimeOnly(9, 0),
                    EndTime = new TimeOnly(12, 0)
                },
                new AvailabilitySlotRequest
                {
                    DayOfWeek = DayOfWeek.Tuesday,
                    StartTime = new TimeOnly(11, 0),
                    EndTime = new TimeOnly(14, 0)
                }
            ]
        };

        var result = await service.UpdateUserAvailabilityAsync(userId, request);

        Assert.True(result.IsFailure);
        Assert.Contains("overlapping", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateUserAvailability_AdjacentNonOverlappingSlotsSameDay_Succeeds()
    {
        var (service, context, _) = CreateTestHarness();

        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Charlie" });
        await context.SaveChangesAsync();

        var request = new UpdateAvailabilityRequest
        {
            Slots =
            [
                new AvailabilitySlotRequest
                {
                    DayOfWeek = DayOfWeek.Wednesday,
                    StartTime = new TimeOnly(9, 0),
                    EndTime = new TimeOnly(11, 0)
                },
                new AvailabilitySlotRequest
                {
                    DayOfWeek = DayOfWeek.Wednesday,
                    StartTime = new TimeOnly(11, 0),
                    EndTime = new TimeOnly(13, 0)
                }
            ]
        };

        var result = await service.UpdateUserAvailabilityAsync(userId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }
}
