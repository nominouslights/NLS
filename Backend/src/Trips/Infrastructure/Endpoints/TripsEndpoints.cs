using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Manifests.Create;
using NorthernLink.Trips.Application.Manifests.GetById;
using NorthernLink.Trips.Application.Manifests.GetManifests;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Infrastructure.Endpoints;

/// <summary>
/// The Trips module's minimal-API surface under <c>/api/trips/manifests</c>. Every
/// endpoint resolves the ambient tenant (401 when absent — the API half of dual tenant
/// enforcement), stamps it onto the command/query, and dispatches via <see cref="ISender"/>.
/// </summary>
public static class TripsEndpoints
{
    public static IEndpointRouteBuilder MapTripsEndpoints(this IEndpointRouteBuilder app)
    {
        var manifests = app.MapGroup("/api/trips/manifests").RequireAuthorization();

        manifests.MapGet("", GetManifests);
        manifests.MapGet("{id:guid}", GetManifestById);
        manifests.MapPost("", CreateManifest);

        return app;
    }

    private static async Task<IResult> GetManifests(
        string? tripNumber, string? unit, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetTripManifestsQuery(tenantId, tripNumber, unit), cancellationToken);
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
            request.Unit ?? string.Empty,
            request.DriverName ?? string.Empty,
            request.DriverLicenceNo,
            request.LicencePlate,
            request.OdometerStartKm,
            request.FuelLevel,
            request.PreTrip ?? [],
            request.Weather ?? [],
            request.TemperatureC,
            request.RoadConditions ?? [],
            request.Visibility,
            request.RoadAdvisories,
            request.Passengers ?? [],
            request.AllSeatbeltsVerified,
            request.Cargo ?? [],
            request.AllCargoSecured,
            request.Issues ?? [],
            request.NoIssues,
            request.DepartureTime,
            request.ArrivalTime,
            request.OdometerEndKm,
            request.TotalKm,
            request.FuelAdded,
            request.FuelLitres,
            request.FuelCostCad,
            request.PostTrip ?? [],
            request.Attestations ?? [],
            request.DriverSignatureName ?? string.Empty,
            request.CertifiedAt,
            request.Source,
            request.EnteredBy);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/trips/manifests/{result.Value}", new ManifestCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }
}

/// <summary>Body of a successful manifest creation (201, with Location header).</summary>
public sealed record ManifestCreatedResponse(Guid Id);

/// <summary>
/// Request body for POST /api/trips/manifests — the full NL-TM-01 form. Enum-typed
/// fields take enum names ("Paper", "ThreeQuarters", "SnowCovered", …); row collections
/// use the same shapes the manifest response returns.
/// </summary>
public sealed record CreateTripManifestRequest(
    DateOnly TripDate,
    string? TripNumber,
    string? Route,
    TripDirection? Direction,
    string? Client,
    string? Unit,
    string? DriverName,
    string? DriverLicenceNo,
    string? LicencePlate,
    int? OdometerStartKm,
    FuelLevel? FuelLevel,
    IReadOnlyList<PreTripChecklistItem>? PreTrip,
    IReadOnlyList<WeatherCondition>? Weather,
    string? TemperatureC,
    IReadOnlyList<RoadCondition>? RoadConditions,
    VisibilityLevel? Visibility,
    string? RoadAdvisories,
    IReadOnlyList<ManifestPassenger>? Passengers,
    bool AllSeatbeltsVerified,
    IReadOnlyList<ManifestCargoItem>? Cargo,
    CargoSecuredStatus? AllCargoSecured,
    IReadOnlyList<string>? Issues,
    bool NoIssues,
    string? DepartureTime,
    string? ArrivalTime,
    int? OdometerEndKm,
    int? TotalKm,
    bool FuelAdded,
    decimal? FuelLitres,
    decimal? FuelCostCad,
    IReadOnlyList<PostTripChecklistItem>? PostTrip,
    IReadOnlyList<bool>? Attestations,
    string? DriverSignatureName,
    DateTimeOffset? CertifiedAt,
    ManifestSource Source,
    string? EnteredBy);
