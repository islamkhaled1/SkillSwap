using SkillSwap.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Text;
using SkillSwap.Application.Abstractions;
namespace SkillSwap.Infrastructure.Repositories
{
    internal class UnitOfWork: IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
