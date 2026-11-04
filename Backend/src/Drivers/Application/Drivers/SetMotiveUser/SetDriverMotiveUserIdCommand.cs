using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.SetMotiveUser;

/// <summary>
/// Sets (non-null) or clears (null) a driver's Motive user id — the key the Motive HOS sync will
/// match on. <c>PUT /api/drivers/{id}/motive</c> sends the value, <c>DELETE</c> sends null.
/// Dispatch plus Compliance Officer (DriverRosterAccess) — see <c>DriversEndpoints</c>.
/// </summary>
public sealed record SetDriverMotiveUserIdCommand(
    Guid TenantId,
    Guid DriverId,
    string? MotiveUserId) : ICommand;
