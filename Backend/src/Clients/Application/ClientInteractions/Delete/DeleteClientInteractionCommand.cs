using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.ClientInteractions.Delete;

/// <summary>
/// Hard-deletes a client interaction. The audit pipeline records a final snapshot plus a
/// synthetic aggregate-deleted journal row, which is what removes the read-model row.
/// </summary>
public sealed record DeleteClientInteractionCommand(Guid TenantId, Guid InteractionId) : ICommand;
