using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.Register;

/// <summary>
/// Registers a record of <paramref name="RecordType"/>: assigns its document number from the
/// tenant's series, computes retention, and indexes it. Idempotent on
/// (<c>Registration.SourceModule</c>, <c>Registration.SourceRecordId</c>) — a replay returns the
/// existing record with <c>Created = false</c>. <paramref name="ActorId"/> is null when the
/// caller is the integration consumer rather than a person. <paramref name="SupersedesSourceRecordId"/>
/// names the same producer's earlier record this one replaces; when that record is registered and
/// active, this registers as its next version (see <c>RecordRegistrar.RegisterOrSupersedeAsync</c>).
/// </summary>
public sealed record RegisterRecordCommand(
    Guid TenantId,
    ComplianceRecordType RecordType,
    RecordRegistration Registration,
    Guid? ActorId,
    string? SupersedesSourceRecordId = null) : ICommand<RegisterRecordResult>;
