using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Abstractions;
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
using NorthernLink.Trips.Application.Trips.Assign;
using NorthernLink.Trips.Application.Trips.ChangeStatus;
using NorthernLink.Trips.Application.Trips.Create;
using NorthernLink.Trips.Application.Trips.GetActivity;
using NorthernLink.Trips.Application.Trips.GetTripById;
using NorthernLink.Trips.Application.Trips.GetTrips;
using NorthernLink.Trips.Application.Trips.RecordDemand;
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
        trips.MapPost("{id:guid}/demand", RecordTripDemand);

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

    private static async Task<IResult> GetTrips(
        DateOnly? date,
        DateOnly? from,
        DateOnly? to,
        TripStatus? status,
        TripServiceType? serviceType,
        Guid? clientId,
        Guid? driverId,
        bool? openOnly,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var filter = new TripFilter(date, from, to, status, serviceType, clientId, driverId, openOnly ?? false);
        var result = await sender.Query(new GetTripsQuery(tenantId, filter), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
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
            request.VehicleUnit,
            request.SeatsCapacity,
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
            request.StopIds ?? [],
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
            request.StopIds ?? [],
            request.DistanceKm,
            request.EstimatedDurationMinutes,
            request.RequiredLicenceClass,
            request.Active);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

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
            request.DaysOfWeek ?? [],
            request.DepartureTime,
            request.ReturnDepartureTime,
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
            request.DaysOfWeek ?? [],
            request.DepartureTime,
            request.ReturnDepartureTime,
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
    string? VehicleUnit,
    int? SeatsCapacity,
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
/// vehicle_lookup (exists + Active) and its unit-number snapshotted server-side.
/// </summary>
public sealed record AssignTripRequest(Guid? DriverId, Guid? VehicleId, string? VehicleUnit);

/// <summary>Request body for POST /api/trips/{id}/status ("InProgress" | "Completed" | "Cancelled").</summary>
public sealed record ChangeTripStatusRequest(TripStatus Status, string? Reason);

/// <summary>Request body for POST /api/trips/{id}/demand.</summary>
public sealed record RecordTripDemandRequest(int SeatsConfirmed, bool DemandGuaranteed);

/// <summary>Request body for POST /api/trips/routes. Stops are chosen from the catalog by id (ordered).</summary>
public sealed record CreateRouteRequest(
    string? Name,
    IReadOnlyList<Guid>? StopIds,
    int DistanceKm,
    int EstimatedDurationMinutes,
    string? RequiredLicenceClass);

/// <summary>Request body for PUT /api/trips/routes/{id} (full row, including active). Stops by id (ordered).</summary>
public sealed record UpdateRouteRequest(
    string? Name,
    IReadOnlyList<Guid>? StopIds,
    int DistanceKm,
    int EstimatedDurationMinutes,
    string? RequiredLicenceClass,
    bool Active);

/// <summary>
/// Request body for POST /api/trips/schedule-templates. Days take day names
/// ("Monday", …); a non-null returnDepartureTime makes each occurrence a paired
/// outbound + return leg.
/// </summary>
public sealed record CreateScheduleTemplateRequest(
    string? Name,
    Guid RouteId,
    TripServiceType ServiceType,
    Guid? ClientId,
    string? ClientName,
    IReadOnlyList<DayOfWeek>? DaysOfWeek,
    TimeOnly DepartureTime,
    TimeOnly? ReturnDepartureTime,
    int SeatsCapacity,
    int? SeatsMinimum,
    string? DefaultVehicleUnit,
    Guid? DefaultDriverId,
    int? GenerationHorizonDays,
    string? CutoffNote);

/// <summary>Request body for PUT /api/trips/schedule-templates/{id} (full row; active via /activate, /deactivate).</summary>
public sealed record UpdateScheduleTemplateRequest(
    string? Name,
    Guid RouteId,
    TripServiceType ServiceType,
    Guid? ClientId,
    string? ClientName,
    IReadOnlyList<DayOfWeek>? DaysOfWeek,
    TimeOnly DepartureTime,
    TimeOnly? ReturnDepartureTime,
    int SeatsCapacity,
    int? SeatsMinimum,
    string? DefaultVehicleUnit,
    Guid? DefaultDriverId,
    int? GenerationHorizonDays,
    string? CutoffNote);
