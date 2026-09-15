using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.UnlinkUser;

/// <summary>
/// Breaks a roster row's Identity link, so that account can no longer act as this driver. The
/// driver and all their compliance history stay — this is a revocation, not a retirement.
/// Dispatch-only — see <c>DriversEndpoints</c>.
/// </summary>
public sealed record UnlinkDriverUserCommand(Guid TenantId, Guid DriverId) : ICommand;
