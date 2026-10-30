using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Application.Trips.ChangeRoute;

/// <summary>
/// Whether a copied stop reference (a passenger's pickup/drop-off, a shipment leg's from/to)
/// still lands on a route's stops. Mirrors the Dispatcher's manifest editor
/// (<c>paxRowsFromManifest</c> in <c>Dispatcher/components/manifest/manifestRows.tsx</c>)
/// exactly, so the preview's count is the number of rows the editor will show blank: match by
/// catalogue <see cref="RouteStop.StopId"/> first, and if that is absent or not found, by exact
/// (ordinal, untrimmed) name.
/// </summary>
public static class RouteStopMatcher
{
    /// <summary>True when nothing was picked — no id and no name — so there is nothing to orphan.</summary>
    public static bool IsUnspecified(Guid? stopId, string? name) =>
        stopId is null && string.IsNullOrEmpty(name);

    public static bool IsOnRoute(Guid? stopId, string? name, IReadOnlyList<RouteStop> stops)
    {
        if (stopId is { } id && stops.Any(stop => stop.StopId == id))
        {
            return true;
        }

        return !string.IsNullOrEmpty(name) && stops.Any(stop => string.Equals(stop.Name, name, StringComparison.Ordinal));
    }

    /// <summary>A reference that was picked and no longer matches any stop on <paramref name="stops"/>.</summary>
    public static bool IsOrphaned(Guid? stopId, string? name, IReadOnlyList<RouteStop> stops) =>
        !IsUnspecified(stopId, name) && !IsOnRoute(stopId, name, stops);
}
