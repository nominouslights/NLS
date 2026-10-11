using Microsoft.Extensions.Logging;
using NorthernLink.Compliance.Application.Records.Register;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Compliance;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Compliance.Application.Integration;

/// <summary>
/// Registers a record for every <see cref="ComplianceRecordProducedIntegrationEvent"/> a producer
/// module (Drivers, Fleet, Trips) writes to its outbox — NL-SP-02 s. 11; HTA s. 318.8(1). Delivery
/// is at-least-once and subscribing replays history, so idempotency is the registrar's
/// (SourceModule, SourceRecordId) key: a replay returns the existing record and registers nothing.
/// Runs outside any HTTP request, so the event's tenant is pushed as the ambient tenant before
/// dispatching (the consumer already does this around scope creation; the push here keeps the
/// handler correct when driven directly).
/// <para>
/// An event naming an unknown record type, subject kind or anchor kind is a producer bug, not a
/// transient fault: it throws, so the polling consumer parks the row as Failed with the message
/// in <c>processing_last_error</c> where an operator can see it, rather than silently skipping.
/// </para>
/// </summary>
public sealed class ComplianceRecordProducedIntegrationEventHandler(
    ISender sender,
    ILogger<ComplianceRecordProducedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<ComplianceRecordProducedIntegrationEvent>
{
    public async Task Handle(ComplianceRecordProducedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (!ComplianceRecordCatalogue.TryParse(integrationEvent.RecordType, out var recordType))
        {
            throw new InvalidOperationException(
                $"compliance.compliance-record-produced {integrationEvent.EventId} names unknown record type '{integrationEvent.RecordType}'.");
        }

        if (!Enum.TryParse<RecordSubjectKind>(integrationEvent.SubjectKind, ignoreCase: false, out var subjectKind))
        {
            throw new InvalidOperationException(
                $"compliance.compliance-record-produced {integrationEvent.EventId} names unknown subject kind '{integrationEvent.SubjectKind}'.");
        }

        var anchors = new Dictionary<AnchorKind, DateOnly>();
        foreach (var (name, date) in integrationEvent.AnchorDates ?? new Dictionary<string, DateOnly>())
        {
            if (!Enum.TryParse<AnchorKind>(name, ignoreCase: false, out var anchor))
            {
                throw new InvalidOperationException(
                    $"compliance.compliance-record-produced {integrationEvent.EventId} names unknown anchor kind '{name}'.");
            }

            anchors[anchor] = date;
        }

        var registration = new RecordRegistration
        {
            Title = integrationEvent.Title,
            Subject = new RecordSubject(subjectKind, integrationEvent.SubjectId, integrationEvent.SubjectDisplay),
            SourceModule = integrationEvent.SourceModule,
            SourceRecordId = integrationEvent.SourceRecordId,
            AnchorDates = anchors,
            SupportingLinks = (integrationEvent.SupportingLinks ?? [])
                .Select(l => new SupportingLink(l.Kind, l.Reference, l.Label))
                .ToList(),
            RegulatoryBasisKeys = integrationEvent.RegulatoryBasisKeys,
            PrintTemplateKey = integrationEvent.PrintTemplateKey,
        };

        using (AmbientTenant.Push(integrationEvent.TenantId))
        {
            var result = await sender.Send(
                new RegisterRecordCommand(integrationEvent.TenantId, recordType, registration, ActorId: null), cancellationToken);

            if (result.IsFailure)
            {
                // A validation refusal (blank title, over-long source id) is also a producer bug —
                // park it visibly for the same reason as an unknown type.
                throw new InvalidOperationException(
                    $"compliance.compliance-record-produced {integrationEvent.EventId} was refused: {result.Error.Code} — {result.Error.Message}");
            }

            logger.LogInformation(
                "Compliance {Action} record {RecordId} ({DocumentNumber}) for {SourceModule}/{SourceRecordId} of tenant {TenantId} ({EventId})",
                result.Value.Created ? "registered" : "already held",
                result.Value.RecordId, result.Value.DocumentNumber,
                integrationEvent.SourceModule, integrationEvent.SourceRecordId, integrationEvent.TenantId, integrationEvent.EventId);
        }
    }
}
