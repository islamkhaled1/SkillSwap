using Microsoft.EntityFrameworkCore;
using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Skills;
using SkillSwap.Domain.Entities;

namespace SkillSwap.Application.Services;

public sealed class CategoryService : ICategoryService
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CategoryService(
        IApplicationDbContext context,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<IReadOnlyList<CategoryDto>>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                IsActive = c.IsActive,
                ActiveSkillsCount = c.Skills.Count(s => s.IsActive),
                CreatedAtUtc = c.CreatedAtUtc
            })
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CategoryDto>>(categories);
    }

    public async Task<Result<CategoryDto>> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .Where(c => c.Id == id && c.IsActive)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                IsActive = c.IsActive,
                ActiveSkillsCount = c.Skills.Count(s => s.IsActive),
                CreatedAtUtc = c.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (category == null)
        {
            return Result.Failure<CategoryDto>($"Category with ID {id} was not found.");
        }

        return Result.Success(category);
    }

    public async Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<CategoryDto>("Category name is required.");
        }

        var trimmedName = request.Name.Trim();
        if (trimmedName.Length > 100)
        {
            return Result.Failure<CategoryDto>("Category name must not exceed 100 characters.");
        }

        var exists = await _context.Categories
            .AnyAsync(c => c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (exists)
        {
            return Result.Failure<CategoryDto>($"A category named '{trimmedName}' already exists.");
        }

        var category = new Category
        {
            Name = trimmedName,
            IsActive = true,
            CreatedAtUtc = _dateTimeProvider.UtcNow
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            IsActive = category.IsActive,
            ActiveSkillsCount = 0,
            CreatedAtUtc = category.CreatedAtUtc
        });
    }
}
