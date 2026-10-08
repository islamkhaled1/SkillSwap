namespace SkillSwap.Domain.Exceptions;

/// <summary>
/// Thrown when an expected entity is not found.
/// </summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object id)
        : base($"{entityName} with id '{id}' was not found.")
    {
    }

    public NotFoundException(string message) : base(message) { }
}
