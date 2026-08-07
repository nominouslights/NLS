using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Shared.Tests;

/// <summary>
/// Paging rides along on <see cref="Result{TValue}"/> so a paged query keeps the same
/// generic signature as an unpaged one. These pin the two properties that depend on:
/// results are unpaged unless explicitly told otherwise, and attaching never fabricates
/// a value onto a failure.
/// </summary>
public class ResultPagingTests
{
    private static readonly Error SomeError = new("Test.Failed", "It failed.", ErrorType.Validation);

    [Fact]
    public void Results_are_unpaged_by_default()
    {
        Assert.Null(Result.Success<IReadOnlyList<int>>([1, 2, 3]).PageInfo);
    }

    [Fact]
    public void WithPage_attaches_metadata_and_preserves_the_value()
    {
        var result = Result.Success<IReadOnlyList<int>>([1, 2, 3])
            .WithPage(new PageInfo(2, 3, 312));

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2, 3], result.Value);
        Assert.Equal(new PageInfo(2, 3, 312), result.PageInfo);
    }

    [Fact]
    public void WithPage_is_a_no_op_on_failure()
    {
        var failed = Result.Failure<IReadOnlyList<int>>(SomeError);

        var result = failed.WithPage(new PageInfo(1, 50, 0));

        Assert.True(result.IsFailure);
        Assert.Equal(SomeError, result.Error);
        Assert.Null(result.PageInfo);
    }

    [Fact]
    public void Unpaged_metadata_makes_the_items_their_own_total()
    {
        var response = PagedResponse<int>.From([1, 2, 3], pageInfo: null);

        Assert.Null(response.Page);
        Assert.Null(response.PageSize);
        Assert.Equal(3, response.TotalCount);
    }

    [Fact]
    public void Paged_metadata_carries_the_unpaged_total_not_the_page_length()
    {
        var response = PagedResponse<int>.From([1, 2, 3], new PageInfo(4, 3, 312));

        Assert.Equal(4, response.Page);
        Assert.Equal(3, response.PageSize);
        Assert.Equal(312, response.TotalCount);
        Assert.Equal(3, response.Items.Count);
    }
}
