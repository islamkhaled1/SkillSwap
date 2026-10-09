using SkillSwap.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Text;

namespace SkillSwap.Infrastructure.Repositories
{
    public class SkillRepository: ISkillRepository
    {
        private readonly ApplicationDbContext _context;

        public SkillRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        private IQueryable<Skill> ActiveSkills()
        {
            return _context.Skills
                .Where(skill =>
                    skill.IsActive &&
                    skill.Category.IsActive);
        }

        public async Task<IReadOnlyList<Skill>> SearchActiveAsync(
            string? searchTerm,
            int? categoryId,
            int skip,
            int take,
            CancellationToken cancellationToken = default)
        {
            var query = ActiveSkills();

            if (categoryId.HasValue)
            {
                query = query.Where(
                    skill => skill.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(skill => skill.Name.Contains(term));
            }

            return await query
                .AsNoTracking()
                .Include(skill => skill.Category)
                .OrderBy(skill => skill.Name)
                .ThenBy(skill => skill.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> CountActiveAsync(
            string? searchTerm,
            int? categoryId,
            CancellationToken cancellationToken = default)
        {
            var query = ActiveSkills();

            if (categoryId.HasValue)
            {
                query = query.Where(
                    skill => skill.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(skill => skill.Name.Contains(term));
            }

            return await query.CountAsync(cancellationToken);
        }

        public async Task<Skill?> GetActiveByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await ActiveSkills()
                .AsNoTracking()
                .Include(skill => skill.Category)
                .FirstOrDefaultAsync(
                    skill => skill.Id == id,
                    cancellationToken);
        }

        public async Task<bool> ExistsInCategoryAsync(
            int categoryId,
            string name,
            CancellationToken cancellationToken = default)
        {
            return await _context.Skills.AnyAsync(
                skill => skill.CategoryId == categoryId &&
                         skill.Name == name,
                cancellationToken);
        }

        public async Task AddAsync(
            Skill skill,
            CancellationToken cancellationToken = default)
        {
            await _context.Skills.AddAsync(skill, cancellationToken);
        }
    }

}
