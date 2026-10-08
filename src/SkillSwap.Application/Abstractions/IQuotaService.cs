using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Wallets;

namespace SkillSwap.Application.Abstractions;

public interface IQuotaService
{
    Task<Result<MonthlyQuotaDto>> GetMonthlyQuotaAsync(Guid userId, DateTime targetUtc, CancellationToken cancellationToken = default);
    Task<int> GetUsedLearningMinutesAsync(Guid userId, DateTime targetUtc, CancellationToken cancellationToken = default);
    Task<int> GetMonthlyLimitMinutesAsync(Guid userId, DateTime targetUtc, CancellationToken cancellationToken = default);
}
