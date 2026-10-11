using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Stops;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Domain.Planning;

/// <summary>
/// Where Northern Link's regulated vehicles are allowed to go: Manitoba, and nothing else.
/// Hard-coded by the owner's decision (2026-10-09; architecture.md §13) — one company, a written
/// policy, no UI toggle: no vehicle crosses a provincial, territorial or international border, and
/// a request that would need one is declined or referred to a partner carrier.
/// <para>
/// Enforced at every point a corridor is written: <see cref="Route.Create"/>/<see cref="Route.Update"/>
/// (a catalog stop outside the area may exist but can never be placed on a route),
/// <see cref="Trip.Schedule"/> and its booking/import/deadhead siblings, <see cref="Trip.Update"/>
/// and <see cref="Trip.ChangeRoute"/> (free-form endpoints and every snapshot stop). Schedule
/// generation inherits the route check. Provinces are compared as <see cref="CanadianProvince"/>
/// codes; a stop with no recorded province (a legacy jsonb row) counts as Manitoba.
/// </para>
/// </summary>
public static class OperatingArea
{
    /// <summary>The province codes the company operates in. Exactly one today.</summary>
    public static readonly IReadOnlyList<string> Provinces = [CanadianProvince.Manitoba];

    /// <summary>The area spelled out for messages — "Manitoba".</summary>
    public static string Description { get; } =
        string.Join(", ", Provinces.Select(CanadianProvince.NameOf));

    /// <summary>Whether <paramref name="provinceCode"/> (a code; null = Manitoba) is inside the operating area.</summary>
    public static bool Covers(string? provinceCode) =>
        Provinces.Contains(provinceCode ?? CanadianProvince.Manitoba, StringComparer.Ordinal);

    /// <summary>
    /// The route-building gate: every stop must carry a known province (null = Manitoba) inside
    /// the area. Returns the stops with their province normalised to its code — the only shape a
    /// route or trip ever stores — in the order given. First failure wins:
    /// <c>Trips.Route.StopUnknownProvince</c>, then <c>Trips.Route.StopOutsideOperatingArea</c>.
    /// </summary>
    public static Result<List<RouteStop>> ValidateRouteStops(IReadOnlyList<RouteStop> stops)
    {
        var normalised = new List<RouteStop>(stops.Count);
        foreach (var stop in stops)
        {
            if (!CanadianProvince.TryNormalizeOrDefault(stop.Province, out var code))
            {
                return Result.Failure<List<RouteStop>>(RouteErrors.StopUnknownProvince(stop.Name, stop.Province!));
            }

            if (!Covers(code))
            {
                return Result.Failure<List<RouteStop>>(RouteErrors.StopOutsideOperatingArea(stop.Name, code!));
            }

            normalised.Add(stop with { Province = code });
        }

        return Result.Success(normalised);
    }

    /// <summary>
    /// The trip gate: the free-form (or snapshotted) origin and destination provinces plus every
    /// snapshot stop must be inside the area. Null provinces default to Manitoba; the result
    /// carries the normalised codes and stops. Checked in the order origin, destination, stops —
    /// the first thing outside the area names the error (<c>Trips.Trip.UnknownProvince</c> /
    /// <c>Trips.Trip.OutsideOperatingArea</c>).
    /// </summary>
    public static Result<TripCorridor> ValidateTripCorridor(
        string origin,
        string? originProvince,
        string destination,
        string? destinationProvince,
        IReadOnlyList<RouteStop> stops)
    {
        var originCheck = CheckEndpoint($"origin \"{origin.Trim()}\"", originProvince);
        if (originCheck.IsFailure)
        {
            return Result.Failure<TripCorridor>(originCheck.Error);
        }

        var destinationCheck = CheckEndpoint($"destination \"{destination.Trim()}\"", destinationProvince);
        if (destinationCheck.IsFailure)
        {
            return Result.Failure<TripCorridor>(destinationCheck.Error);
        }

        var normalisedStops = new List<RouteStop>(stops.Count);
        foreach (var stop in stops)
        {
            var stopCheck = CheckEndpoint($"stop \"{stop.Name.Trim()}\"", stop.Province);
            if (stopCheck.IsFailure)
            {
                return Result.Failure<TripCorridor>(stopCheck.Error);
            }

            normalisedStops.Add(stop with { Province = stopCheck.Value });
        }

        return Result.Success(new TripCorridor(originCheck.Value, destinationCheck.Value, normalisedStops));
    }

    private static Result<string> CheckEndpoint(string which, string? province)
    {
        if (!CanadianProvince.TryNormalizeOrDefault(province, out var code))
        {
            return Result.Failure<string>(TripErrors.UnknownProvince(which, province!));
        }

        return Covers(code)
            ? Result.Success(code!)
            : Result.Failure<string>(TripErrors.OutsideOperatingArea(which, code!));
    }
}

/// <summary>A trip corridor as <see cref="OperatingArea.ValidateTripCorridor"/> hands it back: codes, never free text.</summary>
public sealed record TripCorridor(
    string OriginProvince,
    string DestinationProvince,
    IReadOnlyList<RouteStop> Stops);
