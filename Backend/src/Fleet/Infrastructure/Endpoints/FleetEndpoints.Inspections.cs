using Microsoft.AspNetCore.Http;
using NorthernLink.Fleet.Application.Inspections.Enter;
using NorthernLink.Fleet.Application.Inspections.GetInspections;
using NorthernLink.Fleet.Application.Inspections.Remove;
using NorthernLink.Fleet.Application.Inspections.Update;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Fleet.Infrastructure.Endpoints;

/// <summary>
/// Inspection endpoints: list the vehicle/trip DVIRs, and enter a pre- or post-trip
/// inspection directly (from the trip workflow or, later, the Driver Field App).
/// </summary>
public static partial class FleetEndpoints
{
    private static async Task<IResult> GetInspections(
        string? unit, string? tripNumber, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetVehicleInspectionsQuery(tenantId, unit, tripNumber), cancellationToken);
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
            request.Source ?? InspectionSource.Dispatcher,
            request.Type,
            request.TripNumber,
            request.VehicleId,
            request.Unit ?? string.Empty,
            request.DriverName ?? string.Empty,
            request.EnteredBy,
            request.PerformedAt ?? DateTimeOffset.UtcNow,
            request.OdometerKm,
            request.Checklist ?? [],
            request.Defects ?? [],
            request.Weather ?? [],
            request.TemperatureC,
            request.RoadConditions ?? [],
            request.Visibility,
            request.RoadAdvisories,
            request.FuelLevel,
            request.Issues ?? [],
            request.Attestations ?? [],
            request.DriverSignatureName,
            request.CertifiedAt,
            request.FuelAdded ?? false,
            request.FuelLitres,
            request.FuelCostCad);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/fleet/inspections/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateInspection(
        Guid id, InspectionRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        // Type and TripNumber on the body are ignored: an amend never changes which trip or which
        // half the record is (they are immutable on the aggregate). Everything else is editable.
        var command = new UpdateInspectionCommand(
            id,
            request.Source ?? InspectionSource.Dispatcher,
            request.VehicleId,
            request.Unit ?? string.Empty,
            request.DriverName ?? string.Empty,
            request.EnteredBy,
            request.PerformedAt ?? DateTimeOffset.UtcNow,
            request.OdometerKm,
            request.Checklist ?? [],
            request.Defects ?? [],
            request.Weather ?? [],
            request.TemperatureC,
            request.RoadConditions ?? [],
            request.Visibility,
            request.RoadAdvisories,
            request.FuelLevel,
            request.Issues ?? [],
            request.Attestations ?? [],
            request.DriverSignatureName,
            request.CertifiedAt,
            request.FuelAdded ?? false,
            request.FuelLitres,
            request.FuelCostCad);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RemoveInspection(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new RemoveInspectionCommand(id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }
}

/// <summary>
/// Request body for POST /api/fleet/inspections — a pre- or post-trip DVIR entered from the
/// trip workflow or the Driver App. <see cref="Source"/> defaults to
/// <see cref="InspectionSource.Dispatcher"/>. The pre-trip section (weather/road/visibility/fuel)
/// applies to a <see cref="InspectionType.PreTrip"/>; the post-trip section
/// (issues/attestations/signature/fuel-added) to a <see cref="InspectionType.PostTrip"/>.
/// </summary>
public sealed record InspectionRequest(
    InspectionSource? Source,
    InspectionType Type,
    string? TripNumber,
    Guid? VehicleId,
    string? Unit,
    string? DriverName,
    string? EnteredBy,
    DateTimeOffset? PerformedAt,
    int? OdometerKm,
    IReadOnlyList<ChecklistItemInput>? Checklist,
    IReadOnlyList<DefectInput>? Defects,
    IReadOnlyList<InspectionWeather>? Weather,
    string? TemperatureC,
    IReadOnlyList<InspectionRoadCondition>? RoadConditions,
    InspectionVisibility? Visibility,
    string? RoadAdvisories,
    InspectionFuelLevel? FuelLevel,
    IReadOnlyList<string>? Issues,
    IReadOnlyList<bool>? Attestations,
    string? DriverSignatureName,
    DateTimeOffset? CertifiedAt,
    bool? FuelAdded,
    decimal? FuelLitres,
    decimal? FuelCostCad);
