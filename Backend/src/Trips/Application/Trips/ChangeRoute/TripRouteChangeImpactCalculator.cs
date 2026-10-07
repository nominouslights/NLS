using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Shipments;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.ChangeRoute;

/// <summary>
/// Works out what moving a trip (and, for a round trip, its paired leg) onto another catalogue
/// route would do — the single source of truth behind both the preview query and the change
/// command, so the dialog can never promise something the command then refuses.
/// <list type="bullet">
/// <item><b>Blockers</b> (the command returns the first as its error): the requested trip not
/// Scheduled; any leg that would change confirmed from a community booking day; the route
/// missing, inactive, or the trip's current route; freight already picked up or dropped on any
/// leg that would change.</item>
/// <item><b>Warnings</b> (the command needs <c>acknowledgeWarnings</c>): a paired leg that is no
/// longer Scheduled and so stays on its old route; manifest passengers whose pickup or drop-off
/// is off the new route; Planned shipment legs whose from/to stop is off it; a Bookeo-imported
/// leg.</item>
/// <item><b>Notices</b> (info only): a schedule-generated leg — only this date changes.</item>
/// </list>
/// A Scheduled paired leg is moved too, oriented for its own direction. A paired leg is left
/// alone (<see cref="TripRouteChangeLeg.WillChange"/> false) when it already runs on the target
/// route, or when it has departed, finished, or been cancelled — its run is history, so only the
/// requested trip changes and a warning says the pair no longer mirrors. Nothing about a leg that
/// is left alone can block the change.
/// </summary>
public sealed class TripRouteChangeImpactCalculator(
    ITripRepository trips,
    IRouteRepository routes,
    ITripManifestRepository manifests,
    IShipmentRepository shipments)
{
    public async Task<Result<TripRouteChangeImpact>> CalculateAsync(
        Guid tripId, Guid routeId, CancellationToken cancellationToken)
    {
        var trip = await trips.GetByIdAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure<TripRouteChangeImpact>(TripErrors.NotFound);
        }

        var route = await routes.GetByIdAsync(routeId, cancellationToken);

        var partners = trip.RoundTripKey is { } key
            ? (await trips.GetByRoundTripKeyAsync(key, cancellationToken))
                .Where(other => other.Id != trip.Id)
                .OrderBy(other => other.ServiceDate)
                .ThenBy(other => other.WindowStart)
                .ToList()
            : [];

        var blockers = new List<(Error Error, Trip? Trip)>();
        var warnings = new List<TripRouteChangeFinding>();
        var notices = new List<TripRouteChangeFinding>();

        // The requested trip's own state first — it is the most direct answer to "why not".
        if (trip.Status != TripStatus.Scheduled)
        {
            blockers.Add((TripErrors.RouteChangeNotScheduled, trip));
        }

        if (trip.BookingDayId is not null)
        {
            blockers.Add((TripErrors.RouteOwnedByBooking, trip));
        }

        if (route is null)
        {
            blockers.Add((RouteErrors.NotFound, null));
        }
        else if (trip.RouteId == route.Id)
        {
            blockers.Add((TripErrors.RouteUnchanged, trip));
        }
        else if (!route.Active)
        {
            blockers.Add((TripErrors.RouteInactive, null));
        }

        var legs = new List<TripRouteChangeLeg>();
        foreach (var (leg, isRequested) in partners.Select(p => (p, false)).Prepend((trip, true)))
        {
            var alreadyOnTarget = route is not null && leg.RouteId == route.Id;

            // A paired leg changes with the requested trip only while it is still Scheduled and
            // not already on the target route. One that has departed, finished, or been
            // cancelled keeps the route it ran (or would have run) on.
            var willChange = isRequested || (leg.Status == TripStatus.Scheduled && !alreadyOnTarget);
            var manifest = await manifests.GetForTripAsync(leg.ManifestId, leg.TripNumber, cancellationToken);
            legs.Add(new TripRouteChangeLeg(leg, manifest, isRequested, route is null ? null : leg.RouteSnapshotFor(route), willChange));

            if (!willChange)
            {
                if (route is not null && !alreadyOnTarget)
                {
                    warnings.Add(new TripRouteChangeFinding(
                        TripRouteChangeFindingCodes.PartnerNotChanged,
                        $"The paired leg {leg.TripNumber} is {leg.Status}, so it keeps {leg.RouteName} — only {trip.TripNumber} " +
                        $"moves to {route.Name}, and the two legs will no longer mirror each other.",
                        leg.Id,
                        leg.TripNumber,
                        null));
                }

                // Nothing about a leg that is left alone (its booking, its freight) can block.
                continue;
            }

            if (!isRequested && leg.BookingDayId is not null)
            {
                blockers.Add((TripErrors.RouteChangePartnerOwnedByBooking(leg.TripNumber), leg));
            }

            var legShipments = await shipments.GetForTripAsync(leg.Id, cancellationToken);
            var onThisTrip = legShipments
                .SelectMany(shipment => shipment.Legs
                    .Where(shipmentLeg => shipmentLeg.TripId == leg.Id)
                    .Select(shipmentLeg => (Shipment: shipment, Leg: shipmentLeg)))
                .ToList();

            if (onThisTrip.Any(x => x.Leg.Status is ShipmentLegStatus.PickedUp or ShipmentLegStatus.Dropped))
            {
                blockers.Add((TripErrors.RouteChangeCargoUnderway(leg.TripNumber), leg));
            }

            if (route is not null)
            {
                var offRoutePassengers = manifest?.Passengers.Count(passenger =>
                    RouteStopMatcher.IsOrphaned(passenger.PickupStopId, passenger.PickupStopName, route.Stops)
                    || RouteStopMatcher.IsOrphaned(passenger.DropoffStopId, passenger.DropoffStopName, route.Stops)) ?? 0;
                if (offRoutePassengers > 0)
                {
                    warnings.Add(new TripRouteChangeFinding(
                        TripRouteChangeFindingCodes.PassengerStopsOffRoute,
                        $"{Plural(offRoutePassengers, "passenger")} on {leg.TripNumber} {(offRoutePassengers == 1 ? "has" : "have")} a pickup or drop-off " +
                        $"that is not on {route.Name}. They stay as recorded until re-picked in the manifest editor.",
                        leg.Id,
                        leg.TripNumber,
                        offRoutePassengers));
                }

                // Cancelled freight's planned legs are dead weight, not cargo that will be stranded.
                var offRouteLegs = onThisTrip.Count(x =>
                    x.Shipment.Status != ShipmentStatus.Cancelled
                    && x.Leg.Status == ShipmentLegStatus.Planned
                    && (RouteStopMatcher.IsOrphaned(x.Leg.FromStopId, x.Leg.FromName, route.Stops)
                        || RouteStopMatcher.IsOrphaned(x.Leg.ToStopId, x.Leg.ToName, route.Stops)));
                if (offRouteLegs > 0)
                {
                    warnings.Add(new TripRouteChangeFinding(
                        TripRouteChangeFindingCodes.ShipmentStopsOffRoute,
                        $"{Plural(offRouteLegs, "planned shipment leg")} on {leg.TripNumber} {(offRouteLegs == 1 ? "loads or unloads" : "load or unload")} " +
                        $"at a stop that is not on {route.Name}.",
                        leg.Id,
                        leg.TripNumber,
                        offRouteLegs));
                }
            }

            if (leg.IsCreatedByBookeoImport)
            {
                warnings.Add(new TripRouteChangeFinding(
                    TripRouteChangeFindingCodes.BookeoImported,
                    $"{leg.TripNumber} was imported from Bookeo. A later re-import places its passengers by the route " +
                    "its Bookeo product is mapped to, not by this change.",
                    leg.Id,
                    leg.TripNumber,
                    null));
            }

            if (leg.ScheduleTemplateId is not null)
            {
                notices.Add(new TripRouteChangeFinding(
                    TripRouteChangeFindingCodes.ScheduleGenerated,
                    $"{leg.TripNumber} was generated from a schedule template — only this date changes; the template keeps its route.",
                    leg.Id,
                    leg.TripNumber,
                    null));
            }
        }

        return Result.Success(new TripRouteChangeImpact
        {
            Trip = trip,
            RequestedRouteId = routeId,
            Route = route,
            Legs = legs,
            BlockingErrors = [.. blockers.Select(b => b.Error)],
            Blockers = [.. blockers.Select(b => TripRouteChangeFinding.FromError(b.Error, b.Trip))],
            Warnings = warnings,
            Notices = notices,
        });
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
