using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Profiles;
using SkillSwap.Domain.Entities;

namespace SkillSwap.Application.Services;

/// <summary>
/// Application service implementing user profile retrieval and updates.
/// </summary>
public sealed class ProfileService : IProfileService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ProfileService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<UserProfileDto>> GetMyProfileAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == currentUserId, cancellationToken);

        if (profile == null)
        {
            return Result.Failure<UserProfileDto>($"Profile for user '{currentUserId}' was not found.");
        }

        return Result.Success(MapToDto(profile));
    }

    public async Task<Result<UserProfileDto>> GetProfileByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile == null)
        {
            return Result.Failure<UserProfileDto>($"Profile for user '{userId}' was not found.");
        }

        return Result.Success(MapToDto(profile));
    }

    public async Task<Result<UserProfileDto>> UpdateMyProfileAsync(
        Guid currentUserId,
        UpdateProfileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate DisplayName (1-100 characters)
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Result.Failure<UserProfileDto>("DisplayName is required.");
        }

        var trimmedDisplayName = request.DisplayName.Trim();
        if (trimmedDisplayName.Length > 100)
        {
            return Result.Failure<UserProfileDto>("DisplayName must be between 1 and 100 characters.");
        }

        // 2. Validate Bio (optional, max 1000 characters)
        string? trimmedBio = null;
        if (!string.IsNullOrWhiteSpace(request.Bio))
        {
            trimmedBio = request.Bio.Trim();
            if (trimmedBio.Length > 1000)
            {
                return Result.Failure<UserProfileDto>("Bio cannot exceed 1000 characters.");
            }
        }

        // 3. Validate PhotoUrl (optional, absolute HTTP/HTTPS URL, max 500 characters)
        string? trimmedPhotoUrl = null;
        if (!string.IsNullOrWhiteSpace(request.PhotoUrl))
        {
            trimmedPhotoUrl = request.PhotoUrl.Trim();
            if (trimmedPhotoUrl.Length > 500)
            {
                return Result.Failure<UserProfileDto>("PhotoUrl cannot exceed 500 characters.");
            }

            if (!Uri.TryCreate(trimmedPhotoUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return Result.Failure<UserProfileDto>("PhotoUrl must be a valid absolute HTTP or HTTPS URL.");
            }
        }

        // 4. Validate TimeZoneId against supported system time zones
        if (string.IsNullOrWhiteSpace(request.TimeZoneId))
        {
            return Result.Failure<UserProfileDto>("TimeZoneId is required.");
        }

        var trimmedTimeZoneId = request.TimeZoneId.Trim();
        if (!IsValidTimeZone(trimmedTimeZoneId))
        {
            return Result.Failure<UserProfileDto>($"TimeZoneId '{trimmedTimeZoneId}' is not a valid system time zone.");
        }

        // 5. Query user profile to update
        var profile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == currentUserId, cancellationToken);

        if (profile == null)
        {
            return Result.Failure<UserProfileDto>($"Profile for user '{currentUserId}' was not found.");
        }

        // 6. Mutate only allowed fields
        profile.DisplayName = trimmedDisplayName;
        profile.Bio = trimmedBio;
        profile.PhotoUrl = trimmedPhotoUrl;
        profile.TimeZoneId = trimmedTimeZoneId;
        profile.UpdatedAtUtc = _dateTimeProvider.UtcNow;

        // IsVerified, CreatedAtUtc, and UserId remain untouched by user updates

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToDto(profile));
    }

    private static bool IsValidTimeZone(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static UserProfileDto MapToDto(UserProfile profile) => new()
    {
        UserId = profile.UserId,
        DisplayName = profile.DisplayName,
        Bio = profile.Bio,
        PhotoUrl = profile.PhotoUrl,
        TimeZoneId = profile.TimeZoneId,
        IsVerified = profile.IsVerified,
        CreatedAtUtc = profile.CreatedAtUtc,
        UpdatedAtUtc = profile.UpdatedAtUtc
    };
}
