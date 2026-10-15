using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Availability;
using SkillSwap.Domain.Entities;

namespace SkillSwap.Application.Services;

public sealed class AvailabilityService : IAvailabilityService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AvailabilityService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<IReadOnlyList<AvailabilitySlotDto>>> GetUserAvailabilityAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var slots = await _context.AvailabilitySlots
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .Select(s => new AvailabilitySlotDto
            {
                Id = s.Id,
                UserId = s.UserId,
                DayOfWeek = s.DayOfWeek,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AvailabilitySlotDto>>(slots);
    }

    public async Task<Result<IReadOnlyList<AvailabilitySlotDto>>> UpdateUserAvailabilityAsync(
        Guid userId,
        UpdateAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return Result.Failure<IReadOnlyList<AvailabilitySlotDto>>("Request cannot be null.");
        }

        var userProfileExists = await _context.UserProfiles
            .AnyAsync(p => p.UserId == userId, cancellationToken);

        if (!userProfileExists)
        {
            return Result.Failure<IReadOnlyList<AvailabilitySlotDto>>($"User profile for user {userId} was not found.");
        }

        var slots = request.Slots ?? [];

        // Validate each slot
        foreach (var slot in slots)
        {
            if (!Enum.IsDefined(typeof(DayOfWeek), slot.DayOfWeek))
            {
                return Result.Failure<IReadOnlyList<AvailabilitySlotDto>>(
                    $"Invalid DayOfWeek '{slot.DayOfWeek}'. Must be between Sunday (0) and Saturday (6).");
            }

            if (slot.StartTime >= slot.EndTime)
            {
                return Result.Failure<IReadOnlyList<AvailabilitySlotDto>>(
                    $"Invalid availability slot on {slot.DayOfWeek}: StartTime ({slot.StartTime}) must be strictly earlier than EndTime ({slot.EndTime}).");
            }
        }

        // Validate overlapping intervals on the same day
        var groupedByDay = slots.GroupBy(s => s.DayOfWeek);
        foreach (var group in groupedByDay)
        {
            var sorted = group.OrderBy(s => s.StartTime).ToList();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (sorted[i + 1].StartTime < sorted[i].EndTime)
                {
                    return Result.Failure<IReadOnlyList<AvailabilitySlotDto>>(
                        $"Overlapping availability slots detected on {group.Key}: {sorted[i].StartTime}-{sorted[i].EndTime} overlaps with {sorted[i + 1].StartTime}-{sorted[i + 1].EndTime}.");
                }
            }
        }

        // Replace existing availability slots atomically
        var existingSlots = await _context.AvailabilitySlots
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

        _context.AvailabilitySlots.RemoveRange(existingSlots);

        var newEntities = slots.Select(s => new AvailabilitySlot
        {
            UserId = userId,
            DayOfWeek = s.DayOfWeek,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        }).ToList();

        _context.AvailabilitySlots.AddRange(newEntities);
        await _context.SaveChangesAsync(cancellationToken);

        var resultDtos = newEntities
            .OrderBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartTime)
            .Select(s => new AvailabilitySlotDto
            {
                Id = s.Id,
                UserId = s.UserId,
                DayOfWeek = s.DayOfWeek,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .ToList();

        return Result.Success<IReadOnlyList<AvailabilitySlotDto>>(resultDtos);
    }
}
