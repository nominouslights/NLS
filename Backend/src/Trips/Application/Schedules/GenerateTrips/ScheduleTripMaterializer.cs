using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Schedules;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Schedules.GenerateTrips;

/// <summary>
/// Everything a generation run resolved before writing anything: the template and its
/// route, the driver/vehicle the new trips will be assigned to (null only when there is
/// nothing new to assign), the drafts still to materialize, and how many occurrences in
/// the window already existed. <see cref="Through"/> is inclusive.
/// </summary>
public sealed record ScheduleTripPlan(
    ScheduleTemplate Template,
    Route Route,
    DriverLookup? Driver,
    VehicleLookup? Vehicle,
    IReadOnlyList<TripDraft> Drafts,
    int AlreadyExisted,
    DateOnly From,
    DateOnly Through);

/// <summary>
/// The one code path that turns a schedule template into trips — shared by the background
/// <c>TripGenerationWorker</c> (template horizon, silent) and the dispatcher's on-demand
/// generate/preview (explicit <c>through</c> date, errors shown verbatim). Two steps so a
/// preview runs every guard the real thing would without persisting:
/// <see cref="PlanAsync"/> resolves and validates, <see cref="ApplyAsync"/> mints numbers,
/// builds the aggregates and saves them in one transaction. A plain scoped service over
/// the Application abstractions (no EF here), like <c>ManifestRiderUpserter</c>.
/// </summary>
public sealed class ScheduleTripMaterializer(
    IScheduleTemplateRepository templates,
    IRouteRepository routes,
    IDriverLookupRepository drivers,
    IVehicleLookupRepository vehicles,
    ITripRepository trips,
    ITripNumberGenerator tripNumbers)
{
    /// <summary>
    /// Resolves what a run over <c>[from, through]</c> would create. A null
    /// <paramref name="through"/> means the worker's own window — the template's horizon.
    /// Guards run in this order: template exists → active → window sane → route exists →
    /// then, ONLY when something new would be created, the assignment guards (default
    /// driver set and active, default unit set and an active fleet vehicle) — so a
    /// template already generated through the date re-runs to "0 new" rather than
    /// erroring, exactly as the worker skips a fully materialized template.
    /// </summary>
    public async Task<Result<ScheduleTripPlan>> PlanAsync(
        Guid templateId,
        DateOnly from,
        DateOnly? through,
        CancellationToken cancellationToken)
    {
        var template = await templates.GetByIdAsync(templateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure<ScheduleTripPlan>(ScheduleTemplateErrors.NotFound);
        }

        if (!template.Active)
        {
            return Result.Failure<ScheduleTripPlan>(ScheduleTemplateErrors.TemplateInactive);
        }

        var throughInclusive = through ?? from.AddDays(template.GenerationHorizonDays - 1);
        if (throughInclusive < from || throughInclusive > from.AddDays(ScheduleTemplate.MaxGenerateAheadDays))
        {
            return Result.Failure<ScheduleTripPlan>(ScheduleTemplateErrors.InvalidGenerationWindow);
        }

        var route = await routes.GetByIdAsync(template.RouteId, cancellationToken);
        if (route is null)
        {
            return Result.Failure<ScheduleTripPlan>(ScheduleTemplateErrors.RouteMissing);
        }

        var windowEndExclusive = throughInclusive.AddDays(1);

        // Padded by one day past the window: an overnight (ReturnNextDay) outbound on the
        // window's last day emits its Inbound on windowEnd itself. Without the pad that
        // inbound is never in the "existing" set, gets re-emitted on every run, and trips
        // the unique index — failing the whole save.
        var existing = await trips.GetGeneratedOccurrenceKeysAsync(
            template.Id, from, windowEndExclusive.AddDays(1), cancellationToken);

        var wanted = TripGenerator.Generate(template, NoOccurrences, from, windowEndExclusive);
        var drafts = TripGenerator.Generate(template, existing, from, windowEndExclusive);
        var alreadyExisted = wanted.Count - drafts.Count;

        DriverLookup? driver = null;
        VehicleLookup? vehicle = null;

        if (drafts.Count > 0)
        {
            // A trip is never created unassigned, so a template can only materialize when
            // its defaults resolve to a real, Active driver and fleet vehicle. A template
            // that can't (no default set, driver deactivated, unit renamed or not in the
            // fleet) stays paused until a dispatcher fixes it — a paused template beats
            // another ghost-unit board (the "U-99" cleanup of Aug 2026).
            if (template.DefaultDriverId is not { } defaultDriverId)
            {
                return Result.Failure<ScheduleTripPlan>(ScheduleTemplateErrors.NoDefaultDriver);
            }

            driver = await drivers.GetAsync(defaultDriverId, cancellationToken);
            if (driver is not { IsActive: true })
            {
                return Result.Failure<ScheduleTripPlan>(ScheduleTemplateErrors.DefaultDriverUnavailable);
            }

            if (string.IsNullOrWhiteSpace(template.DefaultVehicleUnit))
            {
                return Result.Failure<ScheduleTripPlan>(ScheduleTemplateErrors.NoDefaultVehicle);
            }

            vehicle = await vehicles.GetByUnitNumberAsync(template.DefaultVehicleUnit, cancellationToken);
            if (vehicle is not { IsActive: true })
            {
                return Result.Failure<ScheduleTripPlan>(ScheduleTemplateErrors.DefaultVehicleUnavailable);
            }
        }

        return Result.Success(new ScheduleTripPlan(
            template, route, driver, vehicle, drafts, alreadyExisted, from, throughInclusive));
    }

    /// <summary>
    /// Materializes the plan's drafts: one trip number per leg, one <see cref="Trip.Schedule"/>
    /// per draft with the template/route/assignment prefilled, one save. A plan with nothing
    /// new succeeds with <c>TripCount 0</c> and touches nothing. A rejected draft fails the
    /// whole template (nothing partial is saved); a unique-index race on save surfaces as
    /// <see cref="ScheduleTemplateErrors.GenerationConflict"/>.
    /// </summary>
    public async Task<Result<ScheduleTripGenerationResult>> ApplyAsync(
        Guid tenantId,
        ScheduleTripPlan plan,
        CancellationToken cancellationToken)
    {
        if (plan.Drafts.Count == 0)
        {
            return Result.Success(Summarize(plan));
        }

        // PlanAsync only leaves these null when there are no drafts.
        var driver = plan.Driver!;
        var vehicle = plan.Vehicle!;
        var template = plan.Template;
        var route = plan.Route;

        var outboundStops = route.Stops.OrderBy(s => s.Order).ToList();
        // Only Order is re-sequenced: both timetable offsets stay attached to their own stop,
        // because the leg's TripDirection — not the position in the list — decides which one
        // applies. Swapping them here would double-reverse the return timetable.
        var returnStops = outboundStops
            .AsEnumerable()
            .Reverse()
            .Select((stop, index) => new RouteStop
            {
                StopId = stop.StopId,
                Name = stop.Name,
                Order = index,
                Latitude = stop.Latitude,
                Longitude = stop.Longitude,
                OutboundOffsetMinutes = stop.OutboundOffsetMinutes,
                ReturnOffsetMinutes = stop.ReturnOffsetMinutes,
            })
            .ToList();

        var built = new List<Trip>(plan.Drafts.Count);
        foreach (var draft in plan.Drafts)
        {
            var outbound = draft.Direction == TripDirection.Outbound;
            var tripNumber = await tripNumbers.NextAsync(tenantId, cancellationToken);

            var tripResult = Trip.Schedule(
                tenantId,
                tripNumber,
                draft.ServiceDate,
                draft.DepartureTime,
                draft.DepartureTime.Add(route.EstimatedDuration),
                template.ServiceType,
                route.Id,
                route.Name,
                outbound ? route.Origin : route.Destination,
                outbound ? route.Destination : route.Origin,
                outbound ? outboundStops : returnStops,
                route.DistanceKm,
                template.Id,
                draft.RoundTripKey,
                draft.Direction,
                isEmptyLeg: false,
                template.ClientId,
                template.ClientName,
                poNumber: null,
                driver.DriverId,
                driver.Name,
                vehicle.VehicleId,
                vehicle.UnitNumber,
                // The fleet vehicle's capacity is server-authoritative, exactly as on
                // ad-hoc creation — the template's manual figure no longer applies.
                // Cargo services carry goods, not passengers: their trips have no seats.
                template.ServiceType.IsCargoService() ? null : vehicle.SeatingCapacity,
                template.SeatsMinimum);

            if (tripResult.IsFailure)
            {
                return Result.Failure<ScheduleTripGenerationResult>(tripResult.Error);
            }

            built.Add(tripResult.Value);
        }

        if (!await trips.TryAddGeneratedAsync(built, cancellationToken))
        {
            return Result.Failure<ScheduleTripGenerationResult>(ScheduleTemplateErrors.GenerationConflict);
        }

        return Result.Success(Summarize(plan));
    }

    /// <summary>The plan's counts as a result — what a preview returns, and what a generate reports once saved.</summary>
    public static ScheduleTripGenerationResult Summarize(ScheduleTripPlan plan) =>
        new(
            plan.From,
            plan.Through,
            plan.Drafts.Count,
            plan.AlreadyExisted,
            plan.Drafts.Count(d => d.Direction == TripDirection.Outbound),
            plan.Drafts.Count(d => d.Direction == TripDirection.Inbound),
            plan.Drafts.Count == 0 ? null : plan.Drafts.Min(d => d.ServiceDate),
            plan.Drafts.Count == 0 ? null : plan.Drafts.Max(d => d.ServiceDate));

    private static readonly HashSet<(DateOnly ServiceDate, TripDirection Direction)> NoOccurrences = [];
}
