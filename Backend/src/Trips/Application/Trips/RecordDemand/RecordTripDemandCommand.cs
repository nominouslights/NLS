using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.RecordDemand;

/// <summary>
/// Records confirmed seat demand on a trip (Manifests screen). DemandGuaranteed is the
/// "gift-a-seat" pledge — a community run that departs regardless of the minimum.
/// </summary>
public sealed record RecordTripDemandCommand(
    Guid TripId,
    int SeatsConfirmed,
    bool DemandGuaranteed) : ICommand;
