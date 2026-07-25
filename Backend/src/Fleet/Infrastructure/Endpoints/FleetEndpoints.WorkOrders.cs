using Microsoft.AspNetCore.Http;
using NorthernLink.Fleet.Application.Services.Add;
using NorthernLink.Fleet.Application.WorkOrders.ChangeStatus;
using NorthernLink.Fleet.Application.WorkOrders.Complete;
using NorthernLink.Fleet.Application.WorkOrders.Create;
using NorthernLink.Fleet.Application.WorkOrders.GetAll;
using NorthernLink.Fleet.Application.WorkOrders.GetForVehicle;
using NorthernLink.Fleet.Domain.Services;
using NorthernLink.Fleet.Domain.WorkOrders;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Fleet.Infrastructure.Endpoints;

/// <summary>Work order endpoints — per-vehicle listing plus the fleet-wide group.</summary>
public static partial class FleetEndpoints
{
    private static async Task<IResult> GetVehicleWorkOrders(
        Guid vehicleId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetVehicleWorkOrdersQuery(tenantId, vehicleId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetAllWorkOrders(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetAllWorkOrdersQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateWorkOrder(
        CreateWorkOrderRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateWorkOrderCommand(
            tenantId,
            request.VehicleId,
            request.Title ?? string.Empty,
            request.Description,
            request.Priority,
            request.Source,
            request.SourceRef,
            request.AssignedTo,
            request.DueDate,
            request.LineItems ?? [],
            request.ShopId,
            request.AuthorizedLimitCad,
            request.BudgetCode,
            request.DateRequiredOrOos,
            request.InspectionId);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/fleet/work-orders/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ChangeWorkOrderStatus(
        Guid id, ChangeWorkOrderStatusRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new ChangeWorkOrderStatusCommand(tenantId, id, request.Status), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CompleteWorkOrder(
        Guid id, CompleteWorkOrderRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CompleteWorkOrderCommand(
            tenantId,
            id,
            request.Date ?? DateTimeOffset.UtcNow,
            request.PerformedBy ?? string.Empty,
            request.Category,
            request.OdometerKm,
            request.ItemsChanged ?? [],
            request.Reason ?? string.Empty,
            request.PartsUsed ?? [],
            request.LaborHours,
            request.CostCad,
            request.Notes);

        var result = await sender.Send(command, cancellationToken);
        // result.Value is the resolving service record's id.
        return result.IsSuccess
            ? Results.Created($"/api/fleet/work-orders/{id}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }
}

/// <summary>Request body for POST /api/fleet/work-orders.</summary>
public sealed record CreateWorkOrderRequest(
    Guid VehicleId,
    string? Title,
    string? Description,
    WorkOrderPriority Priority,
    WorkOrderSource Source,
    string? SourceRef,
    string? AssignedTo,
    DateTimeOffset? DueDate,
    IReadOnlyList<string>? LineItems,
    Guid? ShopId,
    decimal? AuthorizedLimitCad,
    string? BudgetCode,
    DateTimeOffset? DateRequiredOrOos,
    Guid? InspectionId);

/// <summary>Request body for POST /api/fleet/work-orders/{id}/status.</summary>
public sealed record ChangeWorkOrderStatusRequest(WorkOrderStatus Status);

/// <summary>Request body for POST /api/fleet/work-orders/{id}/complete (logs the resolving service).</summary>
public sealed record CompleteWorkOrderRequest(
    DateTimeOffset? Date,
    string? PerformedBy,
    ServiceCategory Category,
    int OdometerKm,
    IReadOnlyList<string>? ItemsChanged,
    string? Reason,
    IReadOnlyList<ServicePartInput>? PartsUsed,
    decimal? LaborHours,
    decimal? CostCad,
    string? Notes);
