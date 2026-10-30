using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.Holds;

/// <summary>Places a hold on a record: it leaves (or never enters) review for disposal until released. Returns the hold id.</summary>
public sealed record PlaceHoldCommand(Guid TenantId, Guid RecordId, string? Reason, Guid? ActorId) : ICommand<Guid>;

/// <summary>Releases one hold by id. The record stays Active; the next nightly sweep decides whether it is due.</summary>
public sealed record ReleaseHoldCommand(Guid TenantId, Guid RecordId, Guid HoldId, Guid? ActorId) : ICommand;
