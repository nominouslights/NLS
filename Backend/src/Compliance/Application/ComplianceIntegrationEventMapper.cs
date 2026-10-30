using NorthernLink.Compliance.Domain.Records.Events;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Compliance;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Application;

/// <summary>
/// Compliance's explicit domain-event → integration-event translation. One public contract
/// today: a registered record announces its issued number
/// (<see cref="ComplianceRecordRegisteredIntegrationEvent"/>) so the producer module can stamp it
/// on the document it holds. Holds, anchors, disposal and rule-set applications stay internal.
/// </summary>
public sealed class ComplianceIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, AggregateRoot aggregate) => domainEvent switch
    {
        ComplianceRecordRegisteredDomainEvent e => new ComplianceRecordRegisteredIntegrationEvent(
            e.TenantId, e.RecordId, e.DocumentNumber, e.RecordType.ToString(), e.SourceModule, e.SourceRecordId),
        _ => null,
    };
}
