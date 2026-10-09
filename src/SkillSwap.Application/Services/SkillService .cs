using SkillSwap.Application.Abstractions;
using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Skills;
using System;
using System.Collections.Generic;
using System.Text;

namespace SkillSwap.Application.Services
{
    public sealed class SkillService : ISkillService
    {
        public sealed class SkillService : ISkillService
        {
            private readonly ISkillRepository _skillRepository;
            private readonly ICategoryRepository _categoryRepository;
            private readonly IUnitOfWork _unitOfWork;

            public SkillService(
                ISkillRepository skillRepository,
                ICategoryRepository categoryRepository,
                IUnitOfWork unitOfWork)
            {
                _skillRepository = skillRepository;
                _categoryRepository = categoryRepository;
                _unitOfWork = unitOfWork;
            }

            public async Task<PagedResult<SkillDto>> SearchAsync(
                string? searchTerm,
                int? categoryId,
                int pageNumber,
                int pageSize,
                CancellationToken cancellationToken = default)
            {
                pageNumber = Math.Max(1, pageNumber);
                pageSize = Math.Clamp(pageSize, 1, 100);

                var totalCount = await _skillRepository.CountActiveAsync(
                    searchTerm,
                    categoryId,
                    cancellationToken);

                var skills = await _skillRepository.SearchActiveAsync(
                    searchTerm,
                    categoryId,
                    (pageNumber - 1) * pageSize,
                    pageSize,
                    cancellationToken);

                var items = skills.Select(skill => new SkillDto(
                    skill.Id,
                    skill.Name,
                    skill.CategoryId,
                    skill.Category.Name)).ToList();

                return new PagedResult<SkillDto>(
                    items,
                    totalCount,
                    pageNumber,
                    pageSize);
            }

            public async Task<Result<SkillDto>> GetByIdAsync(
                int id,
                CancellationToken cancellationToken = default)
            {
                if (id <= 0)
                    return Result.Failure<SkillDto>("Invalid skill ID.");

                var skill = await _skillRepository.GetActiveByIdAsync(
                    id,
                    cancellationToken);

                if (skill is null)
                    return Result.Failure<SkillDto>("Skill not found.");

                return Result.Success(new SkillDto(
                    skill.Id,
                    skill.Name,
                    skill.CategoryId,
                    skill.Category.Name));
            }

            public async Task<Result<SkillDto>> CreateAsync(
                CreateSkillRequest request,
                CancellationToken cancellationToken = default)
            {
                var name = request.Name.Trim();

                var category = await _categoryRepository.GetActiveByIdAsync(
                    request.CategoryId,
                    cancellationToken);

                if (category is null)
                    return Result.Failure<SkillDto>(
                        "The selected category does not exist or is inactive.");

                var alreadyExists = await _skillRepository.ExistsInCategoryAsync(
                    request.CategoryId,
                    name,
                    cancellationToken);

                if (alreadyExists)
                    return Result.Failure<SkillDto>(
                        "A skill with this name already exists in this category.");

                var skill = new Skill
                {
                    Name = name,
                    CategoryId = request.CategoryId,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _skillRepository.AddAsync(skill, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Success(new SkillDto(
                    skill.Id,
                    skill.Name,
                    skill.CategoryId,
                    category.Name));
            }
        }
    }
}
