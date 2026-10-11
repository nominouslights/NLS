using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Qbo.Disconnect;

/// <summary>
/// Disconnects QuickBooks: revokes the grant at Intuit (best effort), deletes the tenant's vault
/// row, and marks the connection Disconnected. The connection row stays as history.
/// <paramref name="ActorId"/> comes from the signed token and rides the event.
/// </summary>
public sealed record DisconnectQboCommand(Guid TenantId, Guid? ActorId) : ICommand;
