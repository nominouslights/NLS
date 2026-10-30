using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.Supersede;

/// <summary>
/// Registers a new version of record <paramref name="RecordId"/> (same type, new number,
/// <c>DocumentVersion + 1</c>) and marks the old one Superseded with today as its Superseded
/// anchor. Returns the successor's id and number.
/// </summary>
public sealed record SupersedeRecordCommand(
    Guid TenantId,
    Guid RecordId,
    RecordRegistration Registration,
    Guid? ActorId) : ICommand<RegisterRecordResult>;
