using System;
using System.Collections.Generic;
using System.Text;

namespace SkillSwap.Application.Abstractions
{
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
    }
}
