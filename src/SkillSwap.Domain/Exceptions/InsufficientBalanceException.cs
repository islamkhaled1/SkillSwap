namespace SkillSwap.Domain.Exceptions;

/// <summary>
/// Thrown when a wallet operation would result in a negative balance.
/// </summary>
public class InsufficientBalanceException : DomainException
{
    public InsufficientBalanceException(int requiredMinutes, int availableMinutes)
        : base($"Insufficient balance. Required: {requiredMinutes} minutes, Available: {availableMinutes} minutes.")
    {
    }
}
