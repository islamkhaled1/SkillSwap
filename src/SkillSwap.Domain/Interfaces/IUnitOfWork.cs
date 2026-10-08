namespace SkillSwap.Domain.Interfaces;

/// <summary>
/// Unit of Work abstraction to coordinate persistence.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
