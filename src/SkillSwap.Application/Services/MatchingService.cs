using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Matching;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Services;

public sealed class MatchingService : IMatchingService
{
    private readonly IApplicationDbContext _context;

    public MatchingService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<MatchRecommendationDto>>> GetMatchesAsync(
        Guid currentUserId,
        int? skillId = null,
        UserSkillLevel? minLevel = null,
        CancellationToken cancellationToken = default)
    {
        List<int> targetSkillIds;

        if (skillId.HasValue)
        {
            var skillExists = await _context.Skills
                .Include(s => s.Category)
                .AnyAsync(s => s.Id == skillId.Value && s.IsActive && s.Category.IsActive, cancellationToken);

            if (!skillExists)
            {
                return Result.Failure<IReadOnlyList<MatchRecommendationDto>>(
                    $"Active skill with ID {skillId.Value} was not found.");
            }

            targetSkillIds = [skillId.Value];
        }
        else
        {
            // Learner matches against skills they want to learn
            targetSkillIds = await _context.UserSkills
                .AsNoTracking()
                .Include(us => us.Skill)
                .ThenInclude(s => s.Category)
                .Where(us => us.UserId == currentUserId
                             && us.Type == UserSkillType.Learn
                             && us.Skill.IsActive
                             && us.Skill.Category.IsActive)
                .Select(us => us.SkillId)
                .ToListAsync(cancellationToken);

            if (targetSkillIds.Count == 0)
            {
                return Result.Success<IReadOnlyList<MatchRecommendationDto>>([]);
            }
        }

        // Exclude blocked users (either blocked by current user or current user is blocked by them)
        var blockedUserIds = await _context.UserBlocks
            .AsNoTracking()
            .Where(b => b.BlockerId == currentUserId || b.BlockedId == currentUserId)
            .Select(b => b.BlockerId == currentUserId ? b.BlockedId : b.BlockerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Query candidate teachers matching target skills
        var candidateQuery = _context.UserSkills
            .AsNoTracking()
            .Include(us => us.Skill)
            .ThenInclude(s => s.Category)
            .Where(us => us.Type == UserSkillType.Teach
                         && targetSkillIds.Contains(us.SkillId)
                         && us.UserId != currentUserId
                         && !blockedUserIds.Contains(us.UserId)
                         && us.Skill.IsActive
                         && us.Skill.Category.IsActive);

        if (minLevel.HasValue)
        {
            candidateQuery = candidateQuery.Where(us => us.Level >= minLevel.Value);
        }

        var candidateUserSkills = await candidateQuery.ToListAsync(cancellationToken);

        if (candidateUserSkills.Count == 0)
        {
            return Result.Success<IReadOnlyList<MatchRecommendationDto>>([]);
        }

        var candidateUserIds = candidateUserSkills.Select(c => c.UserId).Distinct().ToList();

        // Load candidate profiles
        var candidateProfiles = await _context.UserProfiles
            .AsNoTracking()
            .Where(p => candidateUserIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken);

        // Load current user's teach skills for mutual exchange calculation
        var currentUserTeachSkills = await _context.UserSkills
            .AsNoTracking()
            .Include(us => us.Skill)
            .Where(us => us.UserId == currentUserId
                         && us.Type == UserSkillType.Teach
                         && us.Skill.IsActive)
            .ToListAsync(cancellationToken);

        var currentUserTeachSkillIds = currentUserTeachSkills.Select(s => s.SkillId).ToHashSet();

        // Load candidates' learn skills
        var candidatesLearnSkills = await _context.UserSkills
            .AsNoTracking()
            .Include(us => us.Skill)
            .Where(us => candidateUserIds.Contains(us.UserId)
                         && us.Type == UserSkillType.Learn
                         && us.Skill.IsActive)
            .ToListAsync(cancellationToken);

        // Load availability slots
        var currentUserSlots = await _context.AvailabilitySlots
            .AsNoTracking()
            .Where(s => s.UserId == currentUserId)
            .ToListAsync(cancellationToken);

        var candidateSlots = await _context.AvailabilitySlots
            .AsNoTracking()
            .Where(s => candidateUserIds.Contains(s.UserId))
            .ToListAsync(cancellationToken);

        var candidateSlotsLookup = candidateSlots.ToLookup(s => s.UserId);

        var recommendations = new List<MatchRecommendationDto>();

        foreach (var candidateSkill in candidateUserSkills)
        {
            var teacherId = candidateSkill.UserId;
            candidateProfiles.TryGetValue(teacherId, out var profile);

            var displayName = profile?.DisplayName ?? $"User {teacherId.ToString()[..8]}";
            var bio = profile?.Bio;
            var photoUrl = profile?.PhotoUrl;

            // Check reciprocal/mutual skills
            var mutualSkills = candidatesLearnSkills
                .Where(ls => ls.UserId == teacherId && currentUserTeachSkillIds.Contains(ls.SkillId))
                .Select(ls => ls.Skill.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            var isMutualMatch = mutualSkills.Count > 0;

            // Check availability overlap
            var overlaps = new List<AvailabilityOverlapDto>();
            var teacherSlots = candidateSlotsLookup[teacherId];

            foreach (var userSlot in currentUserSlots)
            {
                foreach (var teacherSlot in teacherSlots)
                {
                    if (userSlot.DayOfWeek == teacherSlot.DayOfWeek)
                    {
                        var maxStart = userSlot.StartTime > teacherSlot.StartTime ? userSlot.StartTime : teacherSlot.StartTime;
                        var minEnd = userSlot.EndTime < teacherSlot.EndTime ? userSlot.EndTime : teacherSlot.EndTime;

                        if (maxStart < minEnd)
                        {
                            overlaps.Add(new AvailabilityOverlapDto
                            {
                                DayOfWeek = userSlot.DayOfWeek,
                                OverlapStartTime = maxStart,
                                OverlapEndTime = minEnd
                            });
                        }
                    }
                }
            }

            var uniqueOverlaps = overlaps
                .DistinctBy(o => (o.DayOfWeek, o.OverlapStartTime, o.OverlapEndTime))
                .OrderBy(o => o.DayOfWeek)
                .ThenBy(o => o.OverlapStartTime)
                .ToList();

            // Calculate match score
            var score = 100;

            // Proficiency level scoring
            score += candidateSkill.Level switch
            {
                UserSkillLevel.Beginner => 10,
                UserSkillLevel.Intermediate => 20,
                UserSkillLevel.Advanced => 30,
                _ => 0
            };

            // Mutual exchange bonus
            if (isMutualMatch)
            {
                score += 50;
            }

            // Availability overlap bonus
            if (uniqueOverlaps.Count > 0)
            {
                score += 25;
            }

            recommendations.Add(new MatchRecommendationDto
            {
                TeacherId = teacherId,
                TeacherDisplayName = displayName,
                TeacherBio = bio,
                TeacherPhotoUrl = photoUrl,
                SkillId = candidateSkill.SkillId,
                SkillName = candidateSkill.Skill.Name,
                CategoryId = candidateSkill.Skill.CategoryId,
                CategoryName = candidateSkill.Skill.Category.Name,
                TeacherSkillLevel = candidateSkill.Level,
                IsMutualMatch = isMutualMatch,
                MutualSkills = mutualSkills,
                MatchScore = score,
                OverlappingAvailability = uniqueOverlaps
            });
        }

        var ordered = recommendations
            .OrderByDescending(r => r.MatchScore)
            .ThenBy(r => r.TeacherDisplayName)
            .ThenBy(r => r.SkillName)
            .ToList();

        return Result.Success<IReadOnlyList<MatchRecommendationDto>>(ordered);
    }
}
