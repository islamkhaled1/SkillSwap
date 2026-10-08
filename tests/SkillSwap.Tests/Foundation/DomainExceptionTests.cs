using SkillSwap.Domain.Exceptions;

namespace SkillSwap.Tests.Foundation;

/// <summary>
/// Smoke tests for domain exception types.
/// </summary>
public class DomainExceptionTests
{
    [Fact]
    public void NotFoundException_Message_ContainsEntityAndId()
    {
        var ex = new NotFoundException("User", Guid.Empty);
        Assert.Contains("User", ex.Message);
        Assert.Contains(Guid.Empty.ToString(), ex.Message);
    }

    [Fact]
    public void InsufficientBalanceException_Message_ContainsMinutes()
    {
        var ex = new InsufficientBalanceException(60, 30);
        Assert.Contains("60", ex.Message);
        Assert.Contains("30", ex.Message);
    }

    [Fact]
    public void DomainException_IsException()
    {
        var ex = new DomainException("test error");
        Assert.IsAssignableFrom<Exception>(ex);
        Assert.Equal("test error", ex.Message);
    }
}
