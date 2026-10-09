using SkillSwap.Application.DTOs.Skills;
using System;
using System.Collections.Generic;
using System.Text;

namespace SkillSwap.Application.Abstractions
{
    public interface ICategoryService
    {
        Task<IReadOnlyList<CategoryDto>> GetActiveAsync(
        CancellationToken cancellationToken = default);
    }
}
