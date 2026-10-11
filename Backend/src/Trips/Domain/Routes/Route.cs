using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Routes.Events;

namespace NorthernLink.Trips.Domain.Routes;

/// <summary>
/// A reusable service corridor (e.g. "Thompson ↔ Lynn Lake"): the ordered stops, the
/// distance and running time of one leg, and the licence class the assigned driver must
/// hold. Routes are referenced by schedule templates and snapshotted onto trips —
/// editing a route affects only trips generated after the edit, never history.
/// Deactivating hides a route from new templates/trips without touching either.
/// </summary>
public sealed class Route : AggregateRoot, ITenantScoped
{
    private Route()
    {
        // EF Core materialization only.
        Name = null!;
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public List<RouteStop> Stops { get; private set; } = [];
    public int DistanceKm { get; private set; }
    public TimeSpan EstimatedDuration { get; private set; }
    public string? RequiredLicenceClass { get; private set; }
    public bool Active { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>First stop's name — the outbound leg's starting point.</summary>
    public string Origin => Stops.OrderBy(stop => stop.Order).First().Name;

    /// <summary>Last stop's name — the outbound leg's end point.</summary>
    public string Destination => Stops.OrderBy(stop => stop.Order).Last().Name;

    /// <summary>First stop's province code (null on a legacy snapshot = Manitoba).</summary>
    public string OriginProvince => Stops.OrderBy(stop => stop.Order).First().EffectiveProvince;

    /// <summary>Last stop's province code (null on a legacy snapshot = Manitoba).</summary>
    public string DestinationProvince => Stops.OrderBy(stop => stop.Order).Last().EffectiveProvince;

    /// <summary>
    /// Creates a route. Every stop must lie inside the operating area
    /// (<see cref="Planning.OperatingArea"/> — Manitoba only): a catalog stop elsewhere may exist
    /// but is refused here with <c>Trips.Route.StopOutsideOperatingArea</c>, and the same check
    /// runs on <see cref="Update"/>, so no route can ever carry a stop outside the area.
    /// </summary>
    public static Result<Route> Create(
        Guid tenantId,
        string name,
        IReadOnlyList<RouteStop> stops,
        int distanceKm,
        TimeSpan estimatedDuration,
        string? requiredLicenceClass)
    {
        var validation = Validate(name, stops, distanceKm, estimatedDuration);
        if (validation.IsFailure)
        {
            return Result.Failure<Route>(validation.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var route = new Route
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Stops = NormalizeStops(validation.Value),
            DistanceKm = distanceKm,
            EstimatedDuration = estimatedDuration,
            RequiredLicenceClass = Normalize(requiredLicenceClass),
            Active = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        route.Raise(new RouteCreatedDomainEvent(route.Id));
        return Result.Success(route);
    }

    public Result Update(
        string name,
        IReadOnlyList<RouteStop> stops,
        int distanceKm,
        TimeSpan estimatedDuration,
        string? requiredLicenceClass,
        bool active)
    {
        var validation = Validate(name, stops, distanceKm, estimatedDuration);
        if (validation.IsFailure)
        {
            return Result.Failure(validation.Error);
        }

        Name = name.Trim();
        Stops = NormalizeStops(validation.Value);
        DistanceKm = distanceKm;
        EstimatedDuration = estimatedDuration;
        RequiredLicenceClass = Normalize(requiredLicenceClass);
        Active = active;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new RouteUpdatedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Validates the whole row and hands back the stops with their provinces normalised to codes
    /// (the operating-area gate is the one check that rewrites its input), ready for
    /// <see cref="NormalizeStops"/>.
    /// </summary>
    private static Result<List<RouteStop>> Validate(
        string name,
        IReadOnlyList<RouteStop> stops,
        int distanceKm,
        TimeSpan estimatedDuration)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<List<RouteStop>>(RouteErrors.NameRequired);
        }

        if (stops.Count < 2)
        {
            return Result.Failure<List<RouteStop>>(RouteErrors.AtLeastTwoStops);
        }

        if (stops.Any(stop => string.IsNullOrWhiteSpace(stop.Name)))
        {
            return Result.Failure<List<RouteStop>>(RouteErrors.StopNameRequired);
        }

        // Manitoba-only: a stop outside the operating area can never be placed on a route.
        var area = Planning.OperatingArea.ValidateRouteStops(stops);
        if (area.IsFailure)
        {
            return area;
        }

        if (distanceKm <= 0)
        {
            return Result.Failure<List<RouteStop>>(RouteErrors.InvalidDistance);
        }

        if (estimatedDuration <= TimeSpan.Zero)
        {
            return Result.Failure<List<RouteStop>>(RouteErrors.InvalidDuration);
        }

        // Each leg's timetable is validated in its own travel order: the outbound runs in
        // ascending Order, the return in descending (the last stop is where the return starts).
        var ordered = stops.OrderBy(stop => stop.Order).ToList();

        var outbound = ValidateLeg([.. ordered.Select(stop => stop.OutboundOffsetMinutes)]);
        if (outbound.IsFailure)
        {
            return Result.Failure<List<RouteStop>>(outbound.Error);
        }

        var inbound = ValidateLeg([.. ordered.AsEnumerable().Reverse().Select(stop => stop.ReturnOffsetMinutes)]);
        return inbound.IsFailure
            ? Result.Failure<List<RouteStop>>(inbound.Error)
            : area;
    }

    /// <summary>
    /// Validates one leg's offsets, given in that leg's travel order. A leg is all-or-nothing:
    /// either every stop is untimed (no timetable — the pre-timetable default) or every stop is
    /// timed, starting at 0 and strictly increasing. A partial table is rejected rather than
    /// silently half-applied, because a stop with no time falls back to the trip's departure
    /// and would quietly tell a passenger the wrong hour.
    /// </summary>
    private static Result ValidateLeg(IReadOnlyList<int?> offsetsInTravelOrder)
    {
        if (offsetsInTravelOrder.All(offset => offset is null))
        {
            return Result.Success();
        }

        if (offsetsInTravelOrder.Any(offset => offset is null))
        {
            return Result.Failure(RouteErrors.PartialTimetable);
        }

        if (offsetsInTravelOrder[0] != 0)
        {
            return Result.Failure(RouteErrors.TimetableMustStartAtZero);
        }

        for (var index = 1; index < offsetsInTravelOrder.Count; index++)
        {
            if (offsetsInTravelOrder[index] <= offsetsInTravelOrder[index - 1])
            {
                return Result.Failure(RouteErrors.TimetableNotIncreasing);
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Re-sequences stops 0..n-1 in their given order, trimming names and carrying the
    /// catalog reference (StopId), coordinate snapshot, province code and timetable offsets
    /// through.
    /// </summary>
    private static List<RouteStop> NormalizeStops(IReadOnlyList<RouteStop> stops) =>
        [.. stops
            .OrderBy(stop => stop.Order)
            .Select((stop, index) => new RouteStop
            {
                StopId = stop.StopId,
                Name = stop.Name.Trim(),
                Order = index,
                Latitude = stop.Latitude,
                Longitude = stop.Longitude,
                Province = stop.Province,
                OutboundOffsetMinutes = stop.OutboundOffsetMinutes,
                ReturnOffsetMinutes = stop.ReturnOffsetMinutes,
            })];

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
