using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Persistence.Auditing;
using Xunit;

namespace NorthernLink.Shared.Tests;

public class AuditNamesTests
{
    private sealed record VehicleStatusChangedDomainEvent : IDomainEvent
    {
        public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    }

    private sealed class Vehicle : AggregateRoot;

    private sealed class RetirementCertificate : AggregateRoot;

    [Fact]
    public void ForEvent_strips_suffix_and_kebab_cases()
    {
        Assert.Equal("vehicle-status-changed", AuditNames.ForEvent(typeof(VehicleStatusChangedDomainEvent)));
    }

    [Fact]
    public void ForAggregate_kebab_cases_type_name()
    {
        Assert.Equal("vehicle", AuditNames.ForAggregate(typeof(Vehicle)));
        Assert.Equal("retirement-certificate", AuditNames.ForAggregate(typeof(RetirementCertificate)));
    }
}
