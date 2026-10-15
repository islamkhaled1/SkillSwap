using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Skills;

namespace SkillSwap.Application.Abstractions;

public interface ICategoryService
{
    Task<Result<IReadOnlyList<CategoryDto>>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
}
