using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.LinkUser;

/// <summary>
/// Links a roster row to an Identity user, granting that account the ability to sign into the
/// Driver Field App as this driver. <paramref name="UserId"/> is the account's <c>sub</c>.
/// Dispatch-only — see <c>DriversEndpoints</c>.
/// </summary>
public sealed record LinkDriverUserCommand(Guid TenantId, Guid DriverId, Guid UserId) : ICommand;
