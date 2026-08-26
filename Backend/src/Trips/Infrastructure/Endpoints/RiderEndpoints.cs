using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.Riders.GetRiders;
using NorthernLink.Trips.Application.Riders.SetRotation;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Infrastructure.Endpoints;

/// <summary>
/// Rider-directory surface — <c>/api/trips/riders</c> — called from
/// <see cref="TripsEndpoints"/>. The directory itself is populated by the manifest upsert
/// pipeline, never through an endpoint; the API surface is list + rotation only. Every
/// endpoint resolves the ambient tenant (401 when absent — the API half of dual tenant
/// enforcement), stamps it onto the command/query, and dispatches via <see cref="ISender"/>.
/// </summary>
internal static class RiderEndpoints
{
    public static void MapRiderEndpoints(this IEndpointRouteBuilder app)
    {
        var riders = app.MapGroup("/api/trips/riders").RequireAuthorization();
        riders.MapGet("", GetRiders);
        riders.MapPut("{id:guid}/rotation", SetRotation);
    }

    /// <summary>
    /// Lists the rider directory: 200 <c>RiderResponse[]</c>, ordered
    /// ContractCrew → Community → Nihb → Charter, then name ascending. Optional
    /// <paramref name="serviceType"/> (enum name) and <paramref name="search"/>
    /// (case-insensitive name substring) filters.
    /// </summary>
    private static async Task<IResult> GetRiders(
        TripServiceType? serviceType,
        string? search,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetRidersQuery(tenantId, serviceType, search), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// Sets or clears a rider's crew rotation: 204 on success, 404 for an unknown rider,
    /// 400 when the value isn't 5/10/20 or the rider isn't contract crew.
    /// </summary>
    private static async Task<IResult> SetRotation(
        Guid id,
        SetRiderRotationRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new SetRiderRotationCommand(id, request.RotationDays), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }
}

/// <summary>
/// Request body for PUT /api/trips/riders/{id}/rotation — 5, 10, or 20 days, or null to
/// clear the rotation.
/// </summary>
public sealed record SetRiderRotationRequest(int? RotationDays);
