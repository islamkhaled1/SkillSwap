using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Skills;
using SkillSwap.Domain.Entities;
using SkillSwap.Domain.Enums;

namespace SkillSwap.Application.Services;

public sealed class SkillService : ISkillService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SkillService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<IReadOnlyList<SkillDto>>> GetActiveSkillsAsync(
        int? categoryId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Skills
            .AsNoTracking()
            .Include(s => s.Category)
            .Where(s => s.IsActive && s.Category.IsActive);

        if (categoryId.HasValue)
        {
            query = query.Where(s => s.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(term));
        }

        var skills = await query
            .OrderBy(s => s.Category.Name)
            .ThenBy(s => s.Name)
            .Select(s => new SkillDto
            {
                Id = s.Id,
                CategoryId = s.CategoryId,
                CategoryName = s.Category.Name,
                Name = s.Name,
                IsActive = s.IsActive,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<SkillDto>>(skills);
    }

    public async Task<Result<SkillDto>> GetSkillByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var skill = await _context.Skills
            .AsNoTracking()
            .Include(s => s.Category)
            .Where(s => s.Id == id && s.IsActive && s.Category.IsActive)
            .Select(s => new SkillDto
            {
                Id = s.Id,
                CategoryId = s.CategoryId,
                CategoryName = s.Category.Name,
                Name = s.Name,
                IsActive = s.IsActive,
                CreatedAtUtc = s.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (skill == null)
        {
            return Result.Failure<SkillDto>($"Skill with ID {id} was not found.");
        }

        return Result.Success(skill);
    }

    public async Task<Result<SkillDto>> CreateSkillAsync(CreateSkillRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return Result.Failure<SkillDto>("Request cannot be null.");
        }

        if (request.CategoryId <= 0)
        {
            return Result.Failure<SkillDto>("A valid CategoryId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<SkillDto>("Skill name is required.");
        }

        var trimmedName = request.Name.Trim();
        if (trimmedName.Length > 100)
        {
            return Result.Failure<SkillDto>("Skill name must not exceed 100 characters.");
        }

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken);

        if (category == null)
        {
            return Result.Failure<SkillDto>($"Active category with ID {request.CategoryId} was not found.");
        }

        var exists = await _context.Skills
            .AnyAsync(s => s.CategoryId == request.CategoryId && s.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (exists)
        {
            return Result.Failure<SkillDto>($"Skill '{trimmedName}' already exists in category '{category.Name}'.");
        }

        var skill = new Skill
        {
            CategoryId = request.CategoryId,
            Name = trimmedName,
            IsActive = true,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        };

        _context.Skills.Add(skill);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new SkillDto
        {
            Id = skill.Id,
            CategoryId = skill.CategoryId,
            CategoryName = category.Name,
            Name = skill.Name,
            IsActive = skill.IsActive,
            CreatedAtUtc = skill.CreatedAtUtc
        });
    }

    public async Task<Result<IReadOnlyList<UserSkillDto>>> GetUserSkillsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userSkills = await _context.UserSkills
            .AsNoTracking()
            .Include(us => us.Skill)
            .ThenInclude(s => s.Category)
            .Where(us => us.UserId == userId)
            .OrderBy(us => us.Type)
            .ThenBy(us => us.Skill.Name)
            .Select(us => new UserSkillDto
            {
                Id = us.Id,
                UserId = us.UserId,
                SkillId = us.SkillId,
                SkillName = us.Skill.Name,
                CategoryId = us.Skill.CategoryId,
                CategoryName = us.Skill.Category.Name,
                Type = us.Type,
                Level = us.Level,
                CreatedAtUtc = us.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<UserSkillDto>>(userSkills);
    }

    public async Task<Result<UserSkillDto>> AddUserSkillAsync(Guid userId, AddUserSkillRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return Result.Failure<UserSkillDto>("Request cannot be null.");
        }

        if (request.SkillId <= 0)
        {
            return Result.Failure<UserSkillDto>("A valid SkillId is required.");
        }

        if (!Enum.IsDefined(typeof(UserSkillType), request.Type))
        {
            return Result.Failure<UserSkillDto>("Valid UserSkillType (1=Teach, 2=Learn) is required.");
        }

        if (!Enum.IsDefined(typeof(UserSkillLevel), request.Level))
        {
            return Result.Failure<UserSkillDto>("Valid UserSkillLevel (1=Beginner, 2=Intermediate, 3=Advanced) is required.");
        }

        var skill = await _context.Skills
            .Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.Id == request.SkillId && s.IsActive && s.Category.IsActive, cancellationToken);

        if (skill == null)
        {
            return Result.Failure<UserSkillDto>($"Active skill with ID {request.SkillId} was not found.");
        }

        var userProfileExists = await _context.UserProfiles
            .AnyAsync(p => p.UserId == userId, cancellationToken);

        if (!userProfileExists)
        {
            return Result.Failure<UserSkillDto>($"User profile for user {userId} was not found.");
        }

        var alreadyDeclared = await _context.UserSkills
            .AnyAsync(us => us.UserId == userId && us.SkillId == request.SkillId && us.Type == request.Type, cancellationToken);

        if (alreadyDeclared)
        {
            return Result.Failure<UserSkillDto>($"You have already declared this skill as {request.Type}.");
        }

        var userSkill = new UserSkill
        {
            UserId = userId,
            SkillId = request.SkillId,
            Type = request.Type,
            Level = request.Level,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        };

        _context.UserSkills.Add(userSkill);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new UserSkillDto
        {
            Id = userSkill.Id,
            UserId = userSkill.UserId,
            SkillId = userSkill.SkillId,
            SkillName = skill.Name,
            CategoryId = skill.CategoryId,
            CategoryName = skill.Category.Name,
            Type = userSkill.Type,
            Level = userSkill.Level,
            CreatedAtUtc = userSkill.CreatedAtUtc
        });
    }

    public async Task<Result<UserSkillDto>> UpdateUserSkillAsync(
        Guid userId,
        long userSkillId,
        UpdateUserSkillRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return Result.Failure<UserSkillDto>("Request cannot be null.");
        }

        if (!Enum.IsDefined(typeof(UserSkillLevel), request.Level))
        {
            return Result.Failure<UserSkillDto>("Valid UserSkillLevel (1=Beginner, 2=Intermediate, 3=Advanced) is required.");
        }

        var userSkill = await _context.UserSkills
            .Include(us => us.Skill)
            .ThenInclude(s => s.Category)
            .FirstOrDefaultAsync(us => us.Id == userSkillId, cancellationToken);

        if (userSkill == null)
        {
            return Result.Failure<UserSkillDto>($"User skill declaration with ID {userSkillId} was not found.");
        }

        if (userSkill.UserId != userId)
        {
            return Result.Failure<UserSkillDto>("You are not authorized to update another user's skill declaration.");
        }

        userSkill.Level = request.Level;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new UserSkillDto
        {
            Id = userSkill.Id,
            UserId = userSkill.UserId,
            SkillId = userSkill.SkillId,
            SkillName = userSkill.Skill.Name,
            CategoryId = userSkill.Skill.CategoryId,
            CategoryName = userSkill.Skill.Category.Name,
            Type = userSkill.Type,
            Level = userSkill.Level,
            CreatedAtUtc = userSkill.CreatedAtUtc
        });
    }

    public async Task<Result> RemoveUserSkillAsync(
        Guid userId,
        long userSkillId,
        CancellationToken cancellationToken = default)
    {
        var userSkill = await _context.UserSkills
            .FirstOrDefaultAsync(us => us.Id == userSkillId, cancellationToken);

        if (userSkill == null)
        {
            return Result.Failure($"User skill declaration with ID {userSkillId} was not found.");
        }

        if (userSkill.UserId != userId)
        {
            return Result.Failure("You are not authorized to remove another user's skill declaration.");
        }

        _context.UserSkills.Remove(userSkill);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
