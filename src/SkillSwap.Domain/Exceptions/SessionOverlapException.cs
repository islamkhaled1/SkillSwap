namespace SkillSwap.Domain.Exceptions;

/// <summary>
/// Thrown when a session overlaps with an existing active session for either the teacher or learner.
/// </summary>
public class SessionOverlapException : DomainException
{
    public Guid UserId { get; }
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }

    public SessionOverlapException(Guid userId, DateTime startUtc, DateTime endUtc, string role)
        : base($"Cannot book session. {role} ({userId}) already has an overlapping active session between {startUtc:u} and {endUtc:u}.")
    {
        UserId = userId;
        StartUtc = startUtc;
        EndUtc = endUtc;
    }
}
