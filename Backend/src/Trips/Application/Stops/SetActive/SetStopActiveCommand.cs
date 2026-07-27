using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Stops.SetActive;

/// <summary>
/// Activates or deactivates a stop. Deactivation hides it from new route building only —
/// routes that already snapshotted it are untouched.
/// </summary>
public sealed record SetStopActiveCommand(Guid StopId, bool Active) : ICommand;
