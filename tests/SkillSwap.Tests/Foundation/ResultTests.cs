using SkillSwap.Application.Common;

namespace SkillSwap.Tests.Foundation;

/// <summary>
/// Smoke tests verifying that the Result type works correctly.
/// These serve as foundational sanity checks for the common types.
/// </summary>
public class ResultTests
{
    [Fact]
    public void Result_Success_IsSuccessTrue()
    {
        var result = Result.Success();
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Result_Failure_IsFailureTrue()
    {
        var result = Result.Failure("Something went wrong");
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("Something went wrong", result.Error);
    }

    [Fact]
    public void ResultT_Success_ReturnsValue()
    {
        var result = Result.Success(42);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ResultT_Failure_AccessingValueThrows()
    {
        var result = Result.Failure<int>("error");
        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }
}
