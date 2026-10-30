using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Compliance;

/// <summary>
/// Raised by a <em>producer</em> module (Drivers, Fleet, Trips, later Incidents) whenever it
/// completes something the Safety Plan's s. 11 table says must be kept — a monthly HOS audit, a
/// trip inspection report, a driver abstract review. Routing key
/// <c>compliance.compliance-record-produced</c>. The Compliance module consumes it (outbox
/// polling over each producer's schema) and registers a <c>ComplianceRecord</c>: it assigns the
/// document number, computes the retention date and indexes the record; the producer keeps the
/// actual document and remains its system of record.
/// <para>
/// <see cref="SourceModule"/> + <see cref="SourceRecordId"/> is the idempotency key: delivery is
/// at-least-once and subscribing replays history, so the same pair always converges on one
/// registered record. <see cref="RecordType"/> travels as the <c>ComplianceRecordType</c> enum
/// <em>name</em> ("R9"), never the integer, so a reordered enum cannot silently re-label history.
/// <see cref="AnchorDates"/> is keyed by <c>AnchorKind</c> name ("RecordDate", "MonthEnd", …) for
/// the same reason. <see cref="SubjectKind"/> is a <c>RecordSubjectKind</c> name.
/// </para>
/// <para>
/// <b>Wire compatibility.</b> Append-only, and never mark a member <c>required</c> or
/// <c>[JsonRequired]</c>: an outbox payload is deserialized later by whatever binary runs then,
/// and a required member turns every historical row into a <c>JsonException</c> the polling
/// consumer parks as Failed with no retry (see <c>UserChangedIntegrationEvent</c>). Collections
/// are nullable so an older producer that omits them still binds.
/// </para>
/// </summary>
public sealed record ComplianceRecordProducedIntegrationEvent(
    Guid TenantId,
    string SourceModule,
    string SourceRecordId,
    string RecordType,
    string Title,
    string SubjectKind,
    Guid? SubjectId,
    string SubjectDisplay,
    IReadOnlyDictionary<string, DateOnly>? AnchorDates,
    IReadOnlyList<ComplianceSupportingLinkPayload>? SupportingLinks,
    IReadOnlyList<string>? RegulatoryBasisKeys,
    string? PrintTemplateKey) : IntegrationEvent;

/// <summary>
/// One supporting document reference carried by <see cref="ComplianceRecordProducedIntegrationEvent"/>:
/// <paramref name="Kind"/> says what <paramref name="Reference"/> is ("url", "storage-key",
/// "fleet-document", …), <paramref name="Label"/> is what a person sees.
/// </summary>
public sealed record ComplianceSupportingLinkPayload(string Kind, string Reference, string Label);
