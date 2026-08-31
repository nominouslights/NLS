using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Routes;
using NorthernLink.Trips.Application.Routes.Create;
using NorthernLink.Trips.Application.Routes.GetRoutes;
using NorthernLink.Trips.Application.Routes.Update;
using NorthernLink.Trips.Application.Schedules.Create;
using NorthernLink.Trips.Application.Schedules.GetScheduleTemplates;
using NorthernLink.Trips.Application.Schedules.SetActive;
using NorthernLink.Trips.Application.Schedules.Update;
using NorthernLink.Trips.Application.Stops.Create;
using NorthernLink.Trips.Application.Stops.GetStops;
using NorthernLink.Trips.Application.Stops.SetActive;
using NorthernLink.Trips.Application.Stops.Update;
using NorthernLink.Trips.Application.Trips;
using NorthernLink.Trips.Application.Trips.Assign;
using NorthernLink.Trips.Application.Trips.ChangeStatus;
using NorthernLink.Trips.Application.Trips.CloseWithoutBilling;
using NorthernLink.Trips.Application.Trips.Create;
using NorthernLink.Trips.Application.Trips.CreateDeadheadReturn;
using NorthernLink.Trips.Application.Trips.FinishOperations;
using NorthernLink.Trips.Application.Trips.GetActivity;
using NorthernLink.Trips.Application.Trips.GetTripById;
using NorthernLink.Trips.Application.Trips.GetTrips;
using NorthernLink.Trips.Application.Trips.MergeRoundTrip;
using NorthernLink.Trips.Application.Trips.RecordDemand;
using NorthernLink.Trips.Application.Trips.UnpairRoundTrip;
using NorthernLink.Trips.Application.Trips.Update;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Schedules;
using NorthernLink.Trips.Domain.Stops;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Infrastructure.Endpoints;

/// <summary>
/// Trip planning surface — <c>/api/trips</c> (trips), <c>/api/trips/routes</c>, and
/// <c>/api/trips/schedule-templates</c> — called from <see cref="TripsEndpoints"/>.
/// The <c>{id:guid}</c> constraint on the trip item routes keeps the literal
/// <c>routes</c>/<c>schedule-templates</c>/<c>manifests</c> segments unambiguous.
/// Every endpoint resolves the ambient tenant (401 when absent — the API half of dual
/// tenant enforcement), stamps it onto the command/query, and dispatches via
/// <see cref="ISender"/>.
/// </summary>
internal static class TripPlanningEndpoints
{
    public static void MapTripPlanningEndpoints(this IEndpointRouteBuilder app)
    {
        var trips = app.MapGroup("/api/trips").RequireAuthorization();
        trips.MapGet("", GetTrips);
        trips.MapGet("{id:guid}", GetTripById);
        trips.MapGet("{id:guid}/activity", GetTripActivity);
        trips.MapPost("", CreateTrip);
        trips.MapPut("{id:guid}", UpdateTrip);
        trips.MapPost("{id:guid}/assign", AssignTrip);
        trips.MapPost("{id:guid}/status", ChangeTripStatus);
        trips.MapPost("{id:guid}/finish", FinishTripOperations);
        trips.MapPost("{id:guid}/close-without-billing", CloseTripWithoutBilling);
        trips.MapPost("{id:guid}/demand", RecordTripDemand);
        trips.MapPost("{id:guid}/merge-round-trip", MergeRoundTrip);
        trips.MapPost("{id:guid}/unpair-round-trip", UnpairRoundTrip);
        trips.MapPost("{id:guid}/deadhead-return", CreateDeadheadReturn);

        var routes = app.MapGroup("/api/trips/routes").RequireAuthorization();
        routes.MapGet("", GetRoutes);
        routes.MapPost("", CreateRoute);
        routes.MapPut("{id:guid}", UpdateRoute);

        var stops = app.MapGroup("/api/trips/stops").RequireAuthorization();
        stops.MapGet("", GetStops);
        stops.MapPost("", CreateStop);
        stops.MapPut("{id:guid}", UpdateStop);
        stops.MapPost("{id:guid}/activate", ActivateStop);
        stops.MapPost("{id:guid}/deactivate", DeactivateStop);

        var templates = app.MapGroup("/api/trips/schedule-templates").RequireAuthorization();
        templates.MapGet("", GetScheduleTemplates);
        templates.MapPost("", CreateScheduleTemplate);
        templates.MapPut("{id:guid}", UpdateScheduleTemplate);
        templates.MapPost("{id:guid}/activate", ActivateScheduleTemplate);
        templates.MapPost("{id:guid}/deactivate", DeactivateScheduleTemplate);
    }

    // ---- Trips ----

    /// <summary>Largest page a caller may ask for — a guard on payload size, not a default.</summary>
    private const int MaxPageSize = 200;

    /// <summary>
    /// Lists trips as <c>{ items, page, pageSize, totalCount }</c>. Omitting
    /// <paramref name="page"/>/<paramref name="pageSize"/> returns every match in the same
    /// envelope — callers that need a whole set (a dispatch day, a driver's history) are
    /// never silently truncated by a default page size.
    /// </summary>
    private static async Task<IResult> GetTrips(
        DateOnly? date,
        DateOnly? from,
        DateOnly? to,
        string? status,
        TripServiceType? serviceType,
        Guid? clientId,
        Guid? driverId,
        bool? openOnly,
        bool? assignedOnly,
        bool? excludeCancelled,
        int? page,
        int? pageSize,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        // Matched against the member NAMES, not parsed. Enum.TryParse happily accepts "2"
        // (and IsDefined then confirms it, since 2 is a defined ordinal) — so ?status=2 would
        // still bind by position, and inserting a member would silently repoint every such
        // caller at a different status. Only the spelled-out name gets through.
        TripStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.GetNames<TripStatus>().Contains(status, StringComparer.Ordinal))
            {
                return EndpointResults.Problem(TripErrors.InvalidStatusFilter);
            }

            parsedStatus = Enum.Parse<TripStatus>(status);
        }

        // Clamped rather than rejected — an out-of-range page is a navigation artifact
        // (a filter narrowed the set under the user), not a malformed request.
        var normalizedPage = page is { } p ? Math.Max(1, p) : (int?)null;
        var normalizedPageSize = pageSize is { } s ? Math.Clamp(s, 1, MaxPageSize) : (int?)null;

        // Paging is all-or-nothing: a page number without a size means "unpaged".
        if (normalizedPage is null || normalizedPageSize is null)
        {
            normalizedPage = null;
            normalizedPageSize = null;
        }

        var filter = new TripFilter(
            date,
            from,
            to,
            parsedStatus,
            serviceType,
            clientId,
            driverId,
            openOnly ?? false,
            assignedOnly ?? false,
            excludeCancelled ?? false,
            normalizedPage,
            normalizedPageSize);

        var result = await sender.Query(new GetTripsQuery(tenantId, filter), cancellationToken);
        return result.IsSuccess
            ? Results.Ok(PagedResponse<TripResponse>.From(result.Value, result.PageInfo))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetTripById(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetTripByIdQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetTripActivity(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetTripActivityQuery(id, tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateTrip(
        CreateTripRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateTripCommand(
            tenantId,
            request.ServiceDate,
            request.WindowStart,
            request.WindowEnd,
            request.ServiceType,
            request.RouteId,
            request.RouteName,
            request.Origin,
            request.Destination,
            request.Stops ?? [],
            request.DistanceKm,
            request.Direction,
            request.IsEmptyLeg,
            request.ClientId,
            request.ClientName,
            request.PoNumber,
            request.DriverId,
            request.VehicleId,
            request.SeatsMinimum);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/trips/{result.Value}", new TripCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateTrip(
        Guid id, UpdateTripRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateTripCommand(
            id,
            request.ServiceDate,
            request.WindowStart,
            request.WindowEnd,
            request.ServiceType,
            request.RouteId,
            request.RouteName,
            request.Origin,
            request.Destination,
            request.Stops ?? [],
            request.DistanceKm,
            request.IsEmptyLeg,
            request.ClientId,
            request.ClientName,
            request.PoNumber,
            request.SeatsCapacity,
            request.SeatsMinimum);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> AssignTrip(
        Guid id, AssignTripRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new AssignTripCommand(id, request.DriverId, request.VehicleId, request.VehicleUnit), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ChangeTripStatus(
        Guid id, ChangeTripStatusRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new ChangeTripStatusCommand(id, request.Status, request.Reason), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// Records that the run is over. No body: the resulting status is the trip's to decide —
    /// ReadyForBilling when it has a client, Completed when it doesn't — so there is nothing for
    /// the caller to supply. Re-read the trip to see where it landed.
    /// </summary>
    private static async Task<IResult> FinishTripOperations(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new FinishTripOperationsCommand(id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    /// <summary>Closes out a ReadyForBilling trip that will never be invoiced. Reason required.</summary>
    private static async Task<IResult> CloseTripWithoutBilling(
        Guid id,
        CloseTripWithoutBillingRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new CloseTripWithoutBillingCommand(id, request.Reason), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RecordTripDemand(
        Guid id, RecordTripDemandRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new RecordTripDemandCommand(id, request.SeatsConfirmed, request.DemandGuaranteed), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> MergeRoundTrip(
        Guid id, MergeRoundTripRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new MergeRoundTripCommand(id, request.OtherTripId, request.AllowMismatch, request.Reason),
            cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UnpairRoundTrip(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new UnpairRoundTripCommand(id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateDeadheadReturn(
        Guid id,
        [Microsoft.AspNetCore.Mvc.FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)]
        CreateDeadheadReturnRequest? request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new CreateDeadheadReturnCommand(id, request?.DriverId, request?.VehicleId), cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/trips/{result.Value}", new TripCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    // ---- Routes ----

    private static async Task<IResult> GetRoutes(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetRoutesQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateRoute(
        CreateRouteRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateRouteCommand(
            tenantId,
            request.Name ?? string.Empty,
            ToStopInputs(request.Stops),
            request.DistanceKm,
            request.EstimatedDurationMinutes,
            request.RequiredLicenceClass);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/trips/routes/{result.Value}", new RouteCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateRoute(
        Guid id, UpdateRouteRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateRouteCommand(
            id,
            request.Name ?? string.Empty,
            ToStopInputs(request.Stops),
            request.DistanceKm,
            request.EstimatedDurationMinutes,
            request.RequiredLicenceClass,
            request.Active);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// Maps the wire stop list onto the command's inputs. A missing list becomes empty so the
    /// aggregate's "at least two stops" rule produces the validation error, not a null deref.
    /// </summary>
    private static IReadOnlyList<RouteStopInput> ToStopInputs(IReadOnlyList<RouteStopRequest>? stops) =>
        [.. (stops ?? []).Select(stop => new RouteStopInput(
            stop.StopId,
            stop.OutboundOffsetMinutes,
            stop.ReturnOffsetMinutes))];

    // ---- Stops ----

    private static async Task<IResult> GetStops(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetStopsQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateStop(
        StopRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateStopCommand(
            tenantId,
            request.Name ?? string.Empty,
            request.StopType,
            request.Street,
            request.City ?? string.Empty,
            request.Province ?? string.Empty,
            request.PostalCode,
            request.Country ?? string.Empty,
            request.Latitude,
            request.Longitude,
            request.Notes);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/trips/stops/{result.Value}", new StopCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateStop(
        Guid id, StopRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateStopCommand(
            id,
            request.Name ?? string.Empty,
            request.StopType,
            request.Street,
            request.City ?? string.Empty,
            request.Province ?? string.Empty,
            request.PostalCode,
            request.Country ?? string.Empty,
            request.Latitude,
            request.Longitude,
            request.Notes,
            request.Active ?? true);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ActivateStop(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new SetStopActiveCommand(id, true), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> DeactivateStop(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new SetStopActiveCommand(id, false), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    // ---- Schedule templates ----

    private static async Task<IResult> GetScheduleTemplates(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetScheduleTemplatesQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateScheduleTemplate(
        CreateScheduleTemplateRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateScheduleTemplateCommand(
            tenantId,
            request.Name ?? string.Empty,
            request.RouteId,
            request.ServiceType,
            request.ClientId,
            request.ClientName,
            request.RecurrenceKind,
            request.DaysOfWeek ?? [],
            request.IntervalDays,
            request.AnchorDate,
            request.DaysOfMonth ?? [],
            request.DepartureTime,
            request.ReturnDepartureTime,
            request.ReturnNextDay,
            request.SeatsCapacity,
            request.SeatsMinimum,
            request.DefaultVehicleUnit,
            request.DefaultDriverId,
            request.GenerationHorizonDays ?? ScheduleTemplate.DefaultHorizonDays,
            request.CutoffNote);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created(
                $"/api/trips/schedule-templates/{result.Value}",
                new ScheduleTemplateCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateScheduleTemplate(
        Guid id, UpdateScheduleTemplateRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateScheduleTemplateCommand(
            id,
            request.Name ?? string.Empty,
            request.RouteId,
            request.ServiceType,
            request.ClientId,
            request.ClientName,
            request.RecurrenceKind,
            request.DaysOfWeek ?? [],
            request.IntervalDays,
            request.AnchorDate,
            request.DaysOfMonth ?? [],
            request.DepartureTime,
            request.ReturnDepartureTime,
            request.ReturnNextDay,
            request.SeatsCapacity,
            request.SeatsMinimum,
            request.DefaultVehicleUnit,
            request.DefaultDriverId,
            request.GenerationHorizonDays ?? ScheduleTemplate.DefaultHorizonDays,
            request.CutoffNote);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ActivateScheduleTemplate(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new SetScheduleTemplateActiveCommand(id, true), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> DeactivateScheduleTemplate(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new SetScheduleTemplateActiveCommand(id, false), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }
}

/// <summary>Body of a successful trip creation (201, with Location header).</summary>
public sealed record TripCreatedResponse(Guid Id);

/// <summary>Body of a successful route creation (201, with Location header).</summary>
public sealed record RouteCreatedResponse(Guid Id);

/// <summary>Body of a successful schedule template creation (201, with Location header).</summary>
public sealed record ScheduleTemplateCreatedResponse(Guid Id);

/// <summary>Body of a successful stop creation (201, with Location header).</summary>
public sealed record StopCreatedResponse(Guid Id);

/// <summary>
/// Optional body for POST /api/trips/{id}/deadhead-return. Both fields (and the body
/// itself) may be omitted — the return leg then inherits the source trip's own
/// driver/vehicle. Supply either to send a different driver or unit back; each is
/// validated against its lookup (exists + Active).
/// </summary>
public sealed record CreateDeadheadReturnRequest(Guid? DriverId, Guid? VehicleId);

/// <summary>
/// Request body for POST and PUT /api/trips/stops. <c>StopType</c> takes an enum name
/// ("Hub", "Community", …) or null. <c>Active</c> is only honoured on PUT (a create is
/// always active); toggle it afterwards via /activate and /deactivate.
/// </summary>
public sealed record StopRequest(
    string? Name,
    StopType? StopType,
    string? Street,
    string? City,
    string? Province,
    string? PostalCode,
    string? Country,
    double Latitude,
    double Longitude,
    string? Notes,
    bool? Active);

/// <summary>
/// Request body for POST /api/trips. Enum-typed fields take enum names ("ContractCrew",
/// "Outbound", …). Supplying <c>routeId</c> snapshots the route's corridor fields and
/// ignores the free-form ones; the trip number is always generated server-side.
/// <c>driverId</c> and <c>vehicleId</c> are required — a trip is never created
/// unassigned, and the vehicle must be a real fleet vehicle: its unit number and seating
/// capacity are snapshotted from vehicle_lookup (there is no free-form vehicle unit or
/// manual capacity on creation).
/// </summary>
public sealed record CreateTripRequest(
    DateOnly ServiceDate,
    TimeOnly WindowStart,
    TimeOnly? WindowEnd,
    TripServiceType ServiceType,
    Guid? RouteId,
    string? RouteName,
    string? Origin,
    string? Destination,
    IReadOnlyList<RouteStop>? Stops,
    int DistanceKm,
    TripDirection? Direction,
    bool IsEmptyLeg,
    Guid? ClientId,
    string? ClientName,
    string? PoNumber,
    Guid? DriverId,
    Guid? VehicleId,
    int? SeatsMinimum);

/// <summary>Request body for PUT /api/trips/{id} — editable only while Scheduled.</summary>
public sealed record UpdateTripRequest(
    DateOnly ServiceDate,
    TimeOnly WindowStart,
    TimeOnly? WindowEnd,
    TripServiceType ServiceType,
    Guid? RouteId,
    string? RouteName,
    string? Origin,
    string? Destination,
    IReadOnlyList<RouteStop>? Stops,
    int DistanceKm,
    bool IsEmptyLeg,
    Guid? ClientId,
    string? ClientName,
    string? PoNumber,
    int? SeatsCapacity,
    int? SeatsMinimum);

/// <summary>
/// Request body for POST /api/trips/{id}/assign — null driverId unassigns; null vehicleId
/// with null vehicleUnit clears the vehicle. A non-null vehicleId is validated against
/// vehicle_lookup (exists + Active) and its unit number AND seating capacity snapshotted
/// server-side — assignment re-derives the trip's seatsCapacity, and a vehicle seating
/// fewer than the seats already confirmed is refused
/// (Trips.Trip.VehicleCapacityBelowConfirmed). Clearing the vehicle (or a free-form unit)
/// keeps the last-known capacity.
/// </summary>
public sealed record AssignTripRequest(Guid? DriverId, Guid? VehicleId, string? VehicleUnit);

/// <summary>
/// Request body for POST /api/trips/{id}/status ("InProgress" | "Cancelled"). Finishing a run
/// goes to POST /finish and closing an unbillable one to POST /close-without-billing; Invoiced
/// and the invoice-driven WrittenOff are Billing's to set and are refused here.
/// </summary>
public sealed record ChangeTripStatusRequest(TripStatus Status, string? Reason);

/// <summary>Request body for POST /api/trips/{id}/close-without-billing.</summary>
public sealed record CloseTripWithoutBillingRequest(string Reason);

/// <summary>Request body for POST /api/trips/{id}/demand.</summary>
public sealed record RecordTripDemandRequest(int SeatsConfirmed, bool DemandGuaranteed);

/// <summary>
/// Request body for POST /api/trips/{id}/merge-round-trip — the other leg of the pair,
/// plus the optional <c>allowMismatch</c> manual override (defaults false when omitted),
/// which relaxes only the same-service-date and mirrored-corridor checks.
/// (/unpair-round-trip and /deadhead-return take no body.)
/// <para>
/// <c>reason</c> is optional for two open legs and required when either leg is operationally
/// closed (anything other than Scheduled/InProgress — ReadyForBilling, Invoiced, Completed):
/// omitting it there fails with <c>Trips.Trip.RoundTripReasonRequired</c>, and over 500
/// characters fails with <c>Trips.Trip.RoundTripReasonTooLong</c>. Sensitive pairings are
/// expected to be preceded by a step-up password check against
/// <c>POST /api/identity/auth/verify-password</c>.
/// </para>
/// </summary>
public sealed record MergeRoundTripRequest(
    Guid OtherTripId,
    bool AllowMismatch = false,
    string? Reason = null);

/// <summary>Request body for POST /api/trips/routes. Stops are chosen from the catalog by id (ordered).</summary>
public sealed record CreateRouteRequest(
    string? Name,
    IReadOnlyList<RouteStopRequest>? Stops,
    int DistanceKm,
    int EstimatedDurationMinutes,
    string? RequiredLicenceClass);

/// <summary>Request body for PUT /api/trips/routes/{id} (full row, including active). Stops by id (ordered).</summary>
public sealed record UpdateRouteRequest(
    string? Name,
    IReadOnlyList<RouteStopRequest>? Stops,
    int DistanceKm,
    int EstimatedDurationMinutes,
    string? RequiredLicenceClass,
    bool Active);

/// <summary>
/// One stop in a create/update route body, in corridor order. Both offsets are optional — omit
/// them on every stop for a route with no timetable. Within one leg they are all-or-nothing:
/// supply them for every stop or none, starting at 0 and increasing along that leg's direction
/// of travel (the return leg travels the list backwards, so its zero is the last stop).
/// </summary>
public sealed record RouteStopRequest(
    Guid StopId,
    int? OutboundOffsetMinutes,
    int? ReturnOffsetMinutes);

/// <summary>
/// Request body for POST /api/trips/schedule-templates. <c>RecurrenceKind</c> takes an
/// enum name ("DaysOfWeek" | "EveryNDays" | "MonthlyDays") and selects which of the
/// recurrence fields apply: DaysOfWeek uses <c>daysOfWeek</c> (day names "Monday", …);
/// EveryNDays uses <c>intervalDays</c> + <c>anchorDate</c>; MonthlyDays uses
/// <c>daysOfMonth</c> (1–31, clamped to month-end). Fields for other kinds are ignored.
/// A non-null returnDepartureTime makes each occurrence a paired outbound + return leg.
/// <c>returnNextDay</c> true means the return leg lands on the calendar day AFTER the
/// outbound's (an overnight route) — the return time is then expected to be at or before
/// the outbound's clock time, and the usual same-day ordering check is skipped.
/// </summary>
public sealed record CreateScheduleTemplateRequest(
    string? Name,
    Guid RouteId,
    TripServiceType ServiceType,
    Guid? ClientId,
    string? ClientName,
    ScheduleRecurrenceKind RecurrenceKind,
    IReadOnlyList<DayOfWeek>? DaysOfWeek,
    int? IntervalDays,
    DateOnly? AnchorDate,
    IReadOnlyList<int>? DaysOfMonth,
    TimeOnly DepartureTime,
    TimeOnly? ReturnDepartureTime,
    bool ReturnNextDay,
    int SeatsCapacity,
    int? SeatsMinimum,
    string? DefaultVehicleUnit,
    Guid? DefaultDriverId,
    int? GenerationHorizonDays,
    string? CutoffNote);

/// <summary>Request body for PUT /api/trips/schedule-templates/{id} (full row; active via /activate, /deactivate). See the create request for how <c>recurrenceKind</c> selects the recurrence fields.</summary>
public sealed record UpdateScheduleTemplateRequest(
    string? Name,
    Guid RouteId,
    TripServiceType ServiceType,
    Guid? ClientId,
    string? ClientName,
    ScheduleRecurrenceKind RecurrenceKind,
    IReadOnlyList<DayOfWeek>? DaysOfWeek,
    int? IntervalDays,
    DateOnly? AnchorDate,
    IReadOnlyList<int>? DaysOfMonth,
    TimeOnly DepartureTime,
    TimeOnly? ReturnDepartureTime,
    bool ReturnNextDay,
    int SeatsCapacity,
    int? SeatsMinimum,
    string? DefaultVehicleUnit,
    Guid? DefaultDriverId,
    int? GenerationHorizonDays,
    string? CutoffNote);
