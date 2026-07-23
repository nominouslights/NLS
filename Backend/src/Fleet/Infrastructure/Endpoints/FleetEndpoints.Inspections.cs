using Microsoft.AspNetCore.Http;
using NorthernLink.Fleet.Application.Inspections.Enter;
using NorthernLink.Fleet.Application.Inspections.GetInspections;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Fleet.Infrastructure.Endpoints;

/// <summary>
/// Inspection endpoints: the read side of manifest-materialized DVIRs plus the
/// dispatcher paper-backup entry.
/// </summary>
public static partial class FleetEndpoints
{
    private static async Task<IResult> GetInspections(
        string? unit, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetVehicleInspectionsQuery(tenantId, unit), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> EnterInspection(
        InspectionRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new EnterInspectionCommand(
            tenantId,
            request.Unit ?? string.Empty,
            request.Type,
            request.DriverName ?? string.Empty,
            request.EnteredBy,
            request.PerformedAt ?? DateTimeOffset.UtcNow,
            request.OdometerKm,
            request.Checklist ?? [],
            request.Defects ?? []);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/fleet/inspections/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }
}

/// <summary>Request body for POST /api/fleet/inspections (dispatcher paper-backup DVIR entry).</summary>
public sealed record InspectionRequest(
    string? Unit,
    InspectionType Type,
    string? DriverName,
    string? EnteredBy,
    DateTimeOffset? PerformedAt,
    int? OdometerKm,
    IReadOnlyList<ChecklistItemInput>? Checklist,
    IReadOnlyList<DefectInput>? Defects);
