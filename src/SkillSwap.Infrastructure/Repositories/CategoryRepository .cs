using SkillSwap.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Text;

namespace SkillSwap.Infrastructure.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly ApplicationDbContext _context;

        public CategoryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Category>> GetActiveAsync(
            CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AsNoTracking()
                .Where(category => category.IsActive)
                .OrderBy(category => category.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<Category?> GetActiveByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    category => category.Id == id && category.IsActive,
                    cancellationToken);
        }
    }

}
