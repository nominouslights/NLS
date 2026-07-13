using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Shared.Tests;

public class AggregateRootVersionTests
{
    private sealed class TestAggregate : AggregateRoot;

    [Fact]
    public void New_aggregates_start_at_version_zero()
    {
        Assert.Equal(0, new TestAggregate().Version);
    }

    [Fact]
    public void IncrementVersion_is_monotonic()
    {
        var aggregate = new TestAggregate();

        aggregate.IncrementVersion();
        aggregate.IncrementVersion();

        Assert.Equal(2, aggregate.Version);
    }
}
