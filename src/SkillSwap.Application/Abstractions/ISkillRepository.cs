using System;
using System.Collections.Generic;
using System.Text;

namespace SkillSwap.Application.Abstractions
{
    public interface ISkillRepository
    {
        Task<IReadOnlyList<Skill>> SearchActiveAsync(
        string? searchTerm,
        int? categoryId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

        Task<int> CountActiveAsync(
            string? searchTerm,
            int? categoryId,
            CancellationToken cancellationToken = default);

        Task<Skill?> GetActiveByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsInCategoryAsync(
            int categoryId,
            string name,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            Skill skill,
            CancellationToken cancellationToken = default);
    }
}
