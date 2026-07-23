using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Clearances.Revoke;

/// <summary>Revokes a clearance (hard delete — the audit journal keeps history).</summary>
public sealed record RevokeDriverClearanceCommand(Guid TenantId, Guid ClearanceId) : ICommand;
