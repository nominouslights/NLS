using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.CorrectDriver;

/// <summary>
/// Corrects the driver recorded on a trip whose run is already over (ReadyForBilling, Invoiced,
/// or Completed) — "the trip was logged against the wrong driver". A reason is required. Who made
/// the correction is NOT part of the command: the handler reads it from <c>ICurrentActor</c>, so
/// a caller cannot put a name of its choosing on the audit trail.
/// </summary>
public sealed record CorrectTripDriverCommand(Guid TripId, Guid DriverId, string Reason) : ICommand;
