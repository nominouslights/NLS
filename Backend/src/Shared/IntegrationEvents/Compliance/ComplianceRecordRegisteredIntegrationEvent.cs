using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Compliance;

/// <summary>
/// Published by the Compliance module when a record has been registered and numbered — routing
/// key <c>compliance.compliance-record-registered</c>. Producer modules may consume it to stamp
/// the issued <see cref="DocumentNumber"/> back onto the document they hold (<see cref="SourceModule"/>
/// + <see cref="SourceRecordId"/> says which one), so a printed trip inspection report and the
/// compliance register agree on the same NL-VM-03-2026-0041.
/// <para>
/// Append-only and never <c>required</c> — see <see cref="ComplianceRecordProducedIntegrationEvent"/>.
/// </para>
/// </summary>
public sealed record ComplianceRecordRegisteredIntegrationEvent(
    Guid TenantId,
    Guid RecordId,
    string DocumentNumber,
    string RecordType,
    string SourceModule,
    string SourceRecordId) : IntegrationEvent;
