using SkillSwap.Application.Common;

namespace SkillSwap.Tests.Foundation;

/// <summary>
/// Smoke tests for the PagedResult wrapper.
/// </summary>
public class PagedResultTests
{
    [Fact]
    public void PagedResult_TotalPages_CalculatedCorrectly()
    {
        var result = PagedResult<string>.Create(
            items: ["a", "b", "c"],
            totalCount: 25,
            pageNumber: 1,
            pageSize: 10);

        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void PagedResult_LastPage_HasNoPreviousPage()
    {
        var result = PagedResult<string>.Create(
            items: ["x"],
            totalCount: 25,
            pageNumber: 3,
            pageSize: 10);

        Assert.False(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }
}
