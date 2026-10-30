using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.Dispose;

/// <summary>
/// Confirms destruction of a batch of reviewed records. Each id is disposed or refused with its
/// own reason (not reviewable, held, permanent, not found) — the batch never fails as a whole,
/// and a destruction-log line is written per disposed record in the same save.
/// </summary>
public sealed record DisposeRecordsCommand(
    Guid TenantId,
    IReadOnlyList<Guid> RecordIds,
    string? Method,
    string? Note,
    Guid? ActorId) : ICommand<DisposalResult>;
