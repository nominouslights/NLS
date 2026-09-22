using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Manifests.Create;
using NorthernLink.Trips.Application.Manifests.GetById;
using NorthernLink.Trips.Application.Manifests.GetManifests;
using NorthernLink.Trips.Application.Manifests.Update;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Infrastructure.Endpoints;

/// <summary>
/// The Trips module's minimal-API surface: manifests under <c>/api/trips/manifests</c>
/// (below), plus the trip-planning endpoints (trips, routes, schedule templates) mapped
/// by <see cref="TripPlanningEndpoints"/>. Every endpoint resolves the ambient tenant
/// (401 when absent — the API half of dual tenant enforcement), stamps it onto the
/// command/query, and dispatches via <see cref="ISender"/>.
/// </summary>
public static class TripsEndpoints
{
    public static IEndpointRouteBuilder MapTripsEndpoints(this IEndpointRouteBuilder app)
    {
        // Two sibling groups on one prefix, split by who may do what (see
        // TripPlanningEndpoints for why a nested group cannot widen a narrowed one).
        // Building and editing a manifest is dispatch work…
        var manifestAuthoring = app.MapGroup("/api/trips/manifests")
            .RequireAuthorization(AuthorizationPolicies.DispatchAccess);

        manifestAuthoring.MapPost("", CreateManifest);
        manifestAuthoring.MapPut("{id:guid}", UpdateManifest);

        // …reading one is what the driver does at the door: the manifest IS the passenger list
        // for the run. Reads only — boarding (marking riders on/off) has no endpoint yet; when
        // it lands it belongs on this group, with a caller-owns-this-trip check once Trips can
        // resolve a caller to a driver (see the KNOWN GAP note in TripPlanningEndpoints).
        var manifestReading = app.MapGroup("/api/trips/manifests")
            .RequireAuthorization(AuthorizationPolicies.DriverAccess);

        manifestReading.MapGet("", GetManifests);
        manifestReading.MapGet("{id:guid}", GetManifestById);

        app.MapTripPlanningEndpoints();
        app.MapShipmentEndpoints();
        app.MapRiderEndpoints();

        return app;
    }

    private static async Task<IResult> GetManifests(
        string? tripNumber, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetTripManifestsQuery(tenantId, tripNumber), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetManifestById(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetTripManifestByIdQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateManifest(
        CreateTripManifestRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateTripManifestCommand(
            tenantId,
            request.TripDate,
            request.TripNumber ?? string.Empty,
            request.Route ?? string.Empty,
            request.Direction,
            request.Client,
            request.Passengers ?? [],
            request.AllSeatbeltsVerified,
            request.Cargo ?? [],
            request.AllCargoSecured,
            request.Source,
            request.EnteredBy);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/trips/manifests/{result.Value}", new ManifestCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateManifest(
        Guid id, UpdateTripManifestRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateTripManifestCommand(
            id,
            request.TripDate,
            request.TripNumber ?? string.Empty,
            request.Route ?? string.Empty,
            request.Direction,
            request.Client,
            request.Passengers ?? [],
            request.AllSeatbeltsVerified,
            request.Cargo ?? [],
            request.AllCargoSecured,
            request.Source,
            request.EnteredBy);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }
}

/// <summary>Body of a successful manifest creation (201, with Location header).</summary>
public sealed record ManifestCreatedResponse(Guid Id);

/// <summary>
/// Request body for POST /api/trips/manifests — a passenger + cargo manifest. Enum-typed
/// fields take enum names ("App"/"Dispatcher", "Outbound", "NotApplicable"); passengers and
/// cargo use the shapes the manifest response returns. A Dispatcher-sourced manifest must
/// carry <see cref="EnteredBy"/>.
/// </summary>
public sealed record CreateTripManifestRequest(
    DateOnly TripDate,
    string? TripNumber,
    string? Route,
    TripDirection? Direction,
    string? Client,
    IReadOnlyList<ManifestPassenger>? Passengers,
    bool AllSeatbeltsVerified,
    IReadOnlyList<ManifestCargoItem>? Cargo,
    CargoSecuredStatus? AllCargoSecured,
    ManifestSource Source,
    string? EnteredBy);

/// <summary>
/// Request body for PUT /api/trips/manifests/{id} — revises §1 trip info, passengers, and
/// cargo. Same shape as the create body (minus the id, which is the route parameter).
/// </summary>
public sealed record UpdateTripManifestRequest(
    DateOnly TripDate,
    string? TripNumber,
    string? Route,
    TripDirection? Direction,
    string? Client,
    IReadOnlyList<ManifestPassenger>? Passengers,
    bool AllSeatbeltsVerified,
    IReadOnlyList<ManifestCargoItem>? Cargo,
    CargoSecuredStatus? AllCargoSecured,
    ManifestSource Source,
    string? EnteredBy);
