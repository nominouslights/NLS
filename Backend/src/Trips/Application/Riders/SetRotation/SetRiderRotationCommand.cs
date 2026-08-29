using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Riders.SetRotation;

/// <summary>
/// Sets or clears a contract-crew rider's rotation (5/10/20 days; null clears). The
/// aggregate rejects a rotation on any other service type.
/// </summary>
public sealed record SetRiderRotationCommand(Guid RiderId, int? RotationDays) : ICommand;
