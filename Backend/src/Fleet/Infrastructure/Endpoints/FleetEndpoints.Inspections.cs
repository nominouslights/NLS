using Microsoft.AspNetCore.Http;
using NorthernLink.Fleet.Application.Inspections.AcknowledgeCarrier;
using NorthernLink.Fleet.Application.Inspections.Enter;
using NorthernLink.Fleet.Application.Inspections.GetDefects;
using NorthernLink.Fleet.Application.Inspections.GetInspections;
using NorthernLink.Fleet.Application.Inspections.Remove;
using NorthernLink.Fleet.Application.Inspections.ResolveDefect;
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
            request.FuelCostCad,
            request.CertificationStatement);

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
            request.FuelCostCad,
            request.CertificationStatement);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// A vehicle's defect backlog. An unknown vehicle id returns <c>200 []</c>, not 404 —
    /// consistent with GetVehicleWorkOrders, and correct for a warn-only panel that must never
    /// look like an error.
    /// </summary>
    private static async Task<IResult> GetVehicleDefects(
        Guid vehicleId,
        bool? includeResolved,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(
            new GetVehicleDefectsQuery(tenantId, vehicleId, includeResolved ?? false), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// Clears one defect on this inspection. 404 when the inspection or the item is unknown,
    /// 409 when it was already resolved (resolution is final — in practice this is a
    /// double-click guard, since the panel removes the row optimistically).
    /// </summary>
    private static async Task<IResult> ResolveInspectionDefect(
        Guid id,
        ResolveDefectRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new ResolveInspectionDefectCommand(
            tenantId,
            id,
            request.Item ?? string.Empty,
            request.Reason,
            request.Note,
            request.ResolvedBy);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// Signs the NL-PTI-01 carrier acknowledgement line on this inspection. 404 when the
    /// inspection is unknown (or belongs to another tenant), 400 when the report is not a Fail
    /// (only a Major/OutOfService report is acknowledged) or the name is blank, 409 when it was
    /// already acknowledged — the stamp is final, so this is the double-click guard.
    /// </summary>
    private static async Task<IResult> AcknowledgeInspectionCarrier(
        Guid id,
        CarrierAcknowledgementRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new AcknowledgeInspectionCarrierCommand(
            tenantId,
            id,
            request.AcknowledgedBy,
            request.Note);

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
    decimal? FuelCostCad,
    string? CertificationStatement = null);

/// <summary>
/// Request body for POST /api/fleet/inspections/{id}/defects/resolve. <see cref="Item"/> names
/// the defect (trimmed, case-insensitive) because a defect has no id of its own — it is a jsonb
/// element addressed by <c>(InspectionId, Item)</c>. <see cref="ResolvedBy"/> follows the same
/// client-supplied convention as <c>EnteredBy</c> and defaults to "Dispatch"; the resolution
/// timestamp is stamped server-side and is never taken from the body.
/// </summary>
public sealed record ResolveDefectRequest(
    string? Item,
    DefectResolutionReason Reason,
    string? Note,
    string? ResolvedBy);

/// <summary>
/// Request body for POST /api/fleet/inspections/{id}/carrier-acknowledgement.
/// <see cref="AcknowledgedBy"/> is the carrier representative's name and is REQUIRED — unlike
/// <c>EnteredBy</c>/<c>ResolvedBy</c> it has no "Dispatch" fallback, because the name is the
/// signature. The acknowledgement timestamp is stamped server-side and is never taken from the
/// body.
/// </summary>
public sealed record CarrierAcknowledgementRequest(string? AcknowledgedBy, string? Note);
