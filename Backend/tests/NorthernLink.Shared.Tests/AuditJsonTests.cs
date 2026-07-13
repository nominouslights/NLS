using System.Text.Json;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Persistence.Auditing;
using Xunit;

namespace NorthernLink.Shared.Tests;

public class AuditJsonTests
{
    private enum TestStatus
    {
        Active,
        Retired,
    }

    private sealed class TestAggregate : AggregateRoot
    {
        public string Name { get; set; } = "Unit 12";

        public TestStatus Status { get; set; } = TestStatus.Retired;

        public int OdometerKm { get; set; } = 40_000;

        // Computed property — snapshots must capture these for the viewer.
        public int RemainingKm => 100_000 - OdometerKm;

        public void RaiseSomething() => Raise(new TestDomainEvent());
    }

    private sealed record TestDomainEvent : IDomainEvent
    {
        public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    }

    [Fact]
    public void Serialize_strips_domain_events_from_aggregates()
    {
        var aggregate = new TestAggregate();
        aggregate.RaiseSomething();

        var json = AuditJson.Serialize(aggregate);

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("domainEvents", out _));
    }

    [Fact]
    public void Serialize_uses_camel_case_and_enum_names()
    {
        var json = AuditJson.Serialize(new TestAggregate());

        using var document = JsonDocument.Parse(json);
        Assert.Equal("Retired", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("Unit 12", document.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void Serialize_includes_computed_properties_and_version()
    {
        var json = AuditJson.Serialize(new TestAggregate { OdometerKm = 30_000 });

        using var document = JsonDocument.Parse(json);
        Assert.Equal(70_000, document.RootElement.GetProperty("remainingKm").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("version").GetInt32());
    }
}
