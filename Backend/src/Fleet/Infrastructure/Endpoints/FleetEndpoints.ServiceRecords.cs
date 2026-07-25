using Microsoft.AspNetCore.Http;
using NorthernLink.Fleet.Application.Services.Add;
using NorthernLink.Fleet.Application.Services.GetForVehicle;
using NorthernLink.Fleet.Domain.Services;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Fleet.Infrastructure.Endpoints;

/// <summary>Service history endpoints (NSC Standard 13) — nested under their vehicle.</summary>
public static partial class FleetEndpoints
{
    private static async Task<IResult> GetVehicleServiceRecords(
        Guid vehicleId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetVehicleServiceRecordsQuery(tenantId, vehicleId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> AddServiceRecord(
        Guid vehicleId, ServiceRecordRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new AddServiceRecordCommand(
            tenantId,
            vehicleId,
            request.Date ?? DateTimeOffset.UtcNow,
            request.PerformedBy ?? string.Empty,
            request.Category,
            request.OdometerKm,
            request.ItemsChanged ?? [],
            request.Reason ?? string.Empty,
            request.PartsUsed ?? [],
            request.LaborHours,
            request.CostCad,
            request.WorkOrderId,
            request.Notes);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/fleet/vehicles/{vehicleId}/service-records/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }
}

/// <summary>Request body for POST /api/fleet/vehicles/{vehicleId}/service-records.</summary>
public sealed record ServiceRecordRequest(
    DateTimeOffset? Date,
    string? PerformedBy,
    ServiceCategory Category,
    int OdometerKm,
    IReadOnlyList<string>? ItemsChanged,
    string? Reason,
    IReadOnlyList<ServicePartInput>? PartsUsed,
    decimal? LaborHours,
    decimal? CostCad,
    Guid? WorkOrderId,
    string? Notes);
