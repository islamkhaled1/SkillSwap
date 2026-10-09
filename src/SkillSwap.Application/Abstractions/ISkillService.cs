using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Skills;
using System;
using System.Collections.Generic;
using System.Text;

namespace SkillSwap.Application.Abstractions
{
    public interface ISkillService
    {
        Task<PagedResult<SkillDto>> SearchAsync(
       string? searchTerm,
       int? categoryId,
       int pageNumber,
       int pageSize,
       CancellationToken cancellationToken = default);

        Task<Result<SkillDto>> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<Result<SkillDto>> CreateAsync(
            CreateSkillRequest request,
            CancellationToken cancellationToken = default);
    }
}
