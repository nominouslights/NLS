using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Trips.ChangeRoute;

/// <summary>Codes for the non-blocking findings of a route change (warnings and notices).</summary>
public static class TripRouteChangeFindingCodes
{
    /// <summary>Warning: manifest passengers whose pickup or drop-off is not on the new route. Count = passengers.</summary>
    public const string PassengerStopsOffRoute = "Trips.RouteChange.PassengerStopsOffRoute";

    /// <summary>Warning: Planned shipment legs whose from/to stop is not on the new route. Count = legs.</summary>
    public const string ShipmentStopsOffRoute = "Trips.RouteChange.ShipmentStopsOffRoute";

    /// <summary>Warning: a Bookeo-imported trip — a re-import places passengers by its mapped route.</summary>
    public const string BookeoImported = "Trips.RouteChange.BookeoImported";

    /// <summary>Notice (info only): a schedule-generated trip — only this date changes, not the template.</summary>
    public const string ScheduleGenerated = "Trips.RouteChange.ScheduleGenerated";
}

/// <summary>
/// One finding about a route change. Blockers carry the <see cref="Error"/> the command returns;
/// warnings and notices carry a code from <see cref="TripRouteChangeFindingCodes"/>.
/// <see cref="TripId"/>/<see cref="TripNumber"/> say which leg it is about (null when it is
/// about the request as a whole, e.g. the route not existing); <see cref="Count"/> is set for
/// countable warnings.
/// </summary>
public sealed record TripRouteChangeFinding(
    string Code,
    string Message,
    Guid? TripId,
    string? TripNumber,
    int? Count)
{
    public static TripRouteChangeFinding FromError(Error error, Trip? trip) =>
        new(error.Code, error.Message, trip?.Id, trip?.TripNumber, null);
}

/// <summary>
/// One leg of the change, with what it would look like afterwards (<see cref="NewSnapshot"/> is
/// null only when the target route does not exist). <see cref="WillChange"/> is false for a paired
/// leg that already runs on the target route — it is left as it is.
/// </summary>
public sealed record TripRouteChangeLeg(
    Trip Trip,
    TripManifest? Manifest,
    bool IsRequestedTrip,
    TripRouteSnapshot? NewSnapshot,
    bool WillChange);

/// <summary>
/// Everything a route change would do, computed once by <see cref="TripRouteChangeImpactCalculator"/>
/// and shared by the preview (rendered) and the command (re-checked, then applied). Holds the
/// loaded aggregates so the command mutates exactly what was assessed.
/// </summary>
public sealed class TripRouteChangeImpact
{
    public required Trip Trip { get; init; }
    public required Guid RequestedRouteId { get; init; }
    public Route? Route { get; init; }

    /// <summary>The requested trip first, then its paired leg(s).</summary>
    public required IReadOnlyList<TripRouteChangeLeg> Legs { get; init; }

    public IReadOnlyList<Error> BlockingErrors { get; init; } = [];
    public IReadOnlyList<TripRouteChangeFinding> Blockers { get; init; } = [];
    public IReadOnlyList<TripRouteChangeFinding> Warnings { get; init; } = [];
    public IReadOnlyList<TripRouteChangeFinding> Notices { get; init; } = [];

    public Trip? Partner => Legs.FirstOrDefault(leg => !leg.IsRequestedTrip)?.Trip;

    public bool CanChange => BlockingErrors.Count == 0;

    public bool RequiresAcknowledgement => Warnings.Count > 0;
}
