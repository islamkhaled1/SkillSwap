using SkillSwap.Application.Common;
using SkillSwap.Application.DTOs.Skills;

namespace SkillSwap.Application.Abstractions;

public interface ISkillService
{
    Task<Result<IReadOnlyList<SkillDto>>> GetActiveSkillsAsync(int? categoryId = null, string? search = null, CancellationToken cancellationToken = default);
    Task<Result<SkillDto>> GetSkillByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Result<SkillDto>> CreateSkillAsync(CreateSkillRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<UserSkillDto>>> GetUserSkillsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<UserSkillDto>> AddUserSkillAsync(Guid userId, AddUserSkillRequest request, CancellationToken cancellationToken = default);
    Task<Result<UserSkillDto>> UpdateUserSkillAsync(Guid userId, long userSkillId, UpdateUserSkillRequest request, CancellationToken cancellationToken = default);
    Task<Result> RemoveUserSkillAsync(Guid userId, long userSkillId, CancellationToken cancellationToken = default);
}
