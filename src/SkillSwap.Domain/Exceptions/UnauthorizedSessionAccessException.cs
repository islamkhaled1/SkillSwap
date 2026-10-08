namespace SkillSwap.Domain.Exceptions;

/// <summary>
/// Thrown when a user attempts an action on a session they are not a participant in.
/// </summary>
public class UnauthorizedSessionAccessException : DomainException
{
    public UnauthorizedSessionAccessException(long sessionId, Guid userId)
        : base($"User {userId} is not authorized to modify or access session {sessionId}.")
    {
    }
}
