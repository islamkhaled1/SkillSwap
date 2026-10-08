using SkillSwap.Domain.Enums;

namespace SkillSwap.Domain.Exceptions;

/// <summary>
/// Thrown when an illegal state transition is attempted on a Session.
/// </summary>
public class InvalidSessionStateException : DomainException
{
    public SessionStatus CurrentStatus { get; }
    public string Operation { get; }

    public InvalidSessionStateException(long sessionId, SessionStatus currentStatus, string operation)
        : base($"Cannot perform '{operation}' on session {sessionId} because its current status is '{currentStatus}'.")
    {
        CurrentStatus = currentStatus;
        Operation = operation;
    }
}
