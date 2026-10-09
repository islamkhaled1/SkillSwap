using SkillSwap.Application.Abstractions;
using SkillSwap.Application.DTOs.Skills;
using System;
using System.Collections.Generic;
using System.Text;


namespace SkillSwap.Application.Services
{
    public sealed class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;

        public CategoryService(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<IReadOnlyList<CategoryDto>> GetActiveAsync(
            CancellationToken cancellationToken = default)
        {
            var categories = await _categoryRepository.GetActiveAsync(
                cancellationToken);

            return categories
                .Select(category => new CategoryDto(
                    category.Id,
                    category.Name))
                .ToList();
        }
    }
}
