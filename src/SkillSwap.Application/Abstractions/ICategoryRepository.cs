using System;
using System.Collections.Generic;
using System.Text;

namespace SkillSwap.Application.Abstractions
{
    public interface ICategoryRepository
    {
        Task<IReadOnlyList<Category>> GetActiveAsync(
       CancellationToken cancellationToken = default);

        Task<Category?> GetActiveByIdAsync(
            int id,
            CancellationToken cancellationToken = default);
    }
}
