using SkillSwap.Application.Services;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;
using SkillSwap.Infrastructure.Persistence;
using SkillSwap.Tests.Common;

namespace SkillSwap.Tests.Unit;

public class MatchingServiceTests
{
    private (MatchingService Service, ApplicationDbContext Context) CreateTestHarness()
    {
        var context = TestDbContextFactory.CreateInMemoryContext(Guid.NewGuid().ToString("N"));
        var service = new MatchingService(context);
        return (service, context);
    }

    [Fact]
    public async Task GetMatches_MatchesLearnSkillAgainstTeacherTeachSkill()
    {
        var (service, context) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var otherTeacherId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = learnerId, DisplayName = "Learner" },
            new UserProfile { UserId = teacherId, DisplayName = "Python Pro", Bio = "I teach Python" },
            new UserProfile { UserId = otherTeacherId, DisplayName = "Guitar Guy" }
        );

        var cat = new Category { Name = "Tech", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var pythonSkill = new Skill { CategoryId = cat.Id, Name = "Python", IsActive = true };
        var guitarSkill = new Skill { CategoryId = cat.Id, Name = "Guitar", IsActive = true };
        context.Skills.AddRange(pythonSkill, guitarSkill);
        await context.SaveChangesAsync();

        // Learner wants to learn Python
        context.UserSkills.Add(new UserSkill
        {
            UserId = learnerId,
            SkillId = pythonSkill.Id,
            Type = UserSkillType.Learn,
            Level = UserSkillLevel.Beginner
        });

        // Teacher teaches Python
        context.UserSkills.Add(new UserSkill
        {
            UserId = teacherId,
            SkillId = pythonSkill.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Intermediate
        });

        // Other teacher teaches Guitar
        context.UserSkills.Add(new UserSkill
        {
            UserId = otherTeacherId,
            SkillId = guitarSkill.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Advanced
        });

        await context.SaveChangesAsync();

        var result = await service.GetMatchesAsync(learnerId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        var match = result.Value![0];
        Assert.Equal(teacherId, match.TeacherId);
        Assert.Equal("Python Pro", match.TeacherDisplayName);
        Assert.Equal("Python", match.SkillName);
        Assert.Equal(UserSkillLevel.Intermediate, match.TeacherSkillLevel);
    }

    [Fact]
    public async Task GetMatches_ExcludesCurrentUser_SelfMatchExclusion()
    {
        var (service, context) = CreateTestHarness();

        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Me" });

        var cat = new Category { Name = "Tech", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "Java", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        // User both teaches and wants to learn Java
        context.UserSkills.AddRange(
            new UserSkill { UserId = userId, SkillId = skill.Id, Type = UserSkillType.Learn, Level = UserSkillLevel.Beginner },
            new UserSkill { UserId = userId, SkillId = skill.Id, Type = UserSkillType.Teach, Level = UserSkillLevel.Advanced }
        );
        await context.SaveChangesAsync();

        var result = await service.GetMatchesAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetMatches_ExcludesBlockedUsers_BothDirections()
    {
        var (service, context) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacher1Id = Guid.NewGuid();
        var teacher2Id = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = learnerId, DisplayName = "Learner" },
            new UserProfile { UserId = teacher1Id, DisplayName = "Teacher 1" },
            new UserProfile { UserId = teacher2Id, DisplayName = "Teacher 2" }
        );

        var cat = new Category { Name = "Tech", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "TypeScript", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        context.UserSkills.AddRange(
            new UserSkill { UserId = learnerId, SkillId = skill.Id, Type = UserSkillType.Learn, Level = UserSkillLevel.Beginner },
            new UserSkill { UserId = teacher1Id, SkillId = skill.Id, Type = UserSkillType.Teach, Level = UserSkillLevel.Advanced },
            new UserSkill { UserId = teacher2Id, SkillId = skill.Id, Type = UserSkillType.Teach, Level = UserSkillLevel.Advanced }
        );

        // Learner blocked teacher1; teacher2 blocked learner
        context.UserBlocks.AddRange(
            new UserBlock { BlockerId = learnerId, BlockedId = teacher1Id },
            new UserBlock { BlockerId = teacher2Id, BlockedId = learnerId }
        );
        await context.SaveChangesAsync();

        var result = await service.GetMatchesAsync(learnerId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetMatches_FiltersByMinLevel()
    {
        var (service, context) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var beginnerTeacherId = Guid.NewGuid();
        var advancedTeacherId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = learnerId, DisplayName = "Learner" },
            new UserProfile { UserId = beginnerTeacherId, DisplayName = "Beginner Teacher" },
            new UserProfile { UserId = advancedTeacherId, DisplayName = "Advanced Teacher" }
        );

        var cat = new Category { Name = "Chess", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "Chess Openings", IsActive = true };
        context.Skills.Add(skill);
        await context.SaveChangesAsync();

        context.UserSkills.AddRange(
            new UserSkill { UserId = learnerId, SkillId = skill.Id, Type = UserSkillType.Learn, Level = UserSkillLevel.Beginner },
            new UserSkill { UserId = beginnerTeacherId, SkillId = skill.Id, Type = UserSkillType.Teach, Level = UserSkillLevel.Beginner },
            new UserSkill { UserId = advancedTeacherId, SkillId = skill.Id, Type = UserSkillType.Teach, Level = UserSkillLevel.Advanced }
        );
        await context.SaveChangesAsync();

        var result = await service.GetMatchesAsync(learnerId, minLevel: UserSkillLevel.Intermediate);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(advancedTeacherId, result.Value![0].TeacherId);
    }

    [Fact]
    public async Task GetMatches_CalculatesMutualMatchAndAvailabilityBonuses()
    {
        var (service, context) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = learnerId, DisplayName = "Learner" },
            new UserProfile { UserId = teacherId, DisplayName = "Teacher" }
        );

        var cat = new Category { Name = "General", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skillPiano = new Skill { CategoryId = cat.Id, Name = "Piano", IsActive = true };
        var skillFrench = new Skill { CategoryId = cat.Id, Name = "French", IsActive = true };
        context.Skills.AddRange(skillPiano, skillFrench);
        await context.SaveChangesAsync();

        // Learner learns Piano, teaches French
        context.UserSkills.AddRange(
            new UserSkill { UserId = learnerId, SkillId = skillPiano.Id, Type = UserSkillType.Learn, Level = UserSkillLevel.Beginner },
            new UserSkill { UserId = learnerId, SkillId = skillFrench.Id, Type = UserSkillType.Teach, Level = UserSkillLevel.Advanced }
        );

        // Teacher teaches Piano (Advanced = +30), learns French (Mutual match = +50)
        context.UserSkills.AddRange(
            new UserSkill { UserId = teacherId, SkillId = skillPiano.Id, Type = UserSkillType.Teach, Level = UserSkillLevel.Advanced },
            new UserSkill { UserId = teacherId, SkillId = skillFrench.Id, Type = UserSkillType.Learn, Level = UserSkillLevel.Beginner }
        );

        // Availability Overlap (+25)
        // Learner: Monday 10:00 - 12:00
        // Teacher: Monday 11:00 - 13:00 => overlap 11:00 - 12:00
        context.AvailabilitySlots.AddRange(
            new AvailabilitySlot
            {
                UserId = learnerId,
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(12, 0)
            },
            new AvailabilitySlot
            {
                UserId = teacherId,
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeOnly(11, 0),
                EndTime = new TimeOnly(13, 0)
            }
        );

        await context.SaveChangesAsync();

        var result = await service.GetMatchesAsync(learnerId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        var match = result.Value![0];

        Assert.True(match.IsMutualMatch);
        Assert.Contains("French", match.MutualSkills);
        Assert.Single(match.OverlappingAvailability);
        Assert.Equal(new TimeOnly(11, 0), match.OverlappingAvailability[0].OverlapStartTime);
        Assert.Equal(new TimeOnly(12, 0), match.OverlappingAvailability[0].OverlapEndTime);

        // Base (100) + Advanced (30) + Mutual (50) + Overlap (25) = 205
        Assert.Equal(205, match.MatchScore);
    }

    [Fact]
    public async Task GetMatches_DeterministicOrdering_ByScoreDescending()
    {
        var (service, context) = CreateTestHarness();

        var learnerId = Guid.NewGuid();
        var teacherLowScore = Guid.NewGuid();
        var teacherHighScore = Guid.NewGuid();

        context.UserProfiles.AddRange(
            new UserProfile { UserId = learnerId, DisplayName = "Learner" },
            new UserProfile { UserId = teacherLowScore, DisplayName = "Low Score Teacher" },
            new UserProfile { UserId = teacherHighScore, DisplayName = "High Score Teacher" }
        );

        var cat = new Category { Name = "General", IsActive = true };
        context.Categories.Add(cat);
        await context.SaveChangesAsync();

        var skill = new Skill { CategoryId = cat.Id, Name = "Math", IsActive = true };
        var skillArt = new Skill { CategoryId = cat.Id, Name = "Art", IsActive = true };
        context.Skills.AddRange(skill, skillArt);
        await context.SaveChangesAsync();

        // Learner learns Math, teaches Art
        context.UserSkills.AddRange(
            new UserSkill { UserId = learnerId, SkillId = skill.Id, Type = UserSkillType.Learn, Level = UserSkillLevel.Beginner },
            new UserSkill { UserId = learnerId, SkillId = skillArt.Id, Type = UserSkillType.Teach, Level = UserSkillLevel.Advanced }
        );

        // Low score: Beginner level (100 + 10 = 110)
        context.UserSkills.Add(new UserSkill
        {
            UserId = teacherLowScore,
            SkillId = skill.Id,
            Type = UserSkillType.Teach,
            Level = UserSkillLevel.Beginner
        });

        // High score: Advanced level + mutual Art (100 + 30 + 50 = 180)
        context.UserSkills.AddRange(
            new UserSkill { UserId = teacherHighScore, SkillId = skill.Id, Type = UserSkillType.Teach, Level = UserSkillLevel.Advanced },
            new UserSkill { UserId = teacherHighScore, SkillId = skillArt.Id, Type = UserSkillType.Learn, Level = UserSkillLevel.Beginner }
        );

        await context.SaveChangesAsync();

        var result = await service.GetMatchesAsync(learnerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal(teacherHighScore, result.Value[0].TeacherId);
        Assert.Equal(teacherLowScore, result.Value[1].TeacherId);
        Assert.True(result.Value[0].MatchScore > result.Value[1].MatchScore);
    }

    [Fact]
    public async Task GetMatches_WhenSpecificSkillIdNotFound_ReturnsFailure()
    {
        var (service, _) = CreateTestHarness();

        var result = await service.GetMatchesAsync(Guid.NewGuid(), skillId: 9999);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetMatches_WhenUserHasNoLearnSkillsAndNoSkillId_ReturnsEmptyList()
    {
        var (service, context) = CreateTestHarness();

        var userId = Guid.NewGuid();
        context.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "No Skills" });
        await context.SaveChangesAsync();

        var result = await service.GetMatchesAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }
}
