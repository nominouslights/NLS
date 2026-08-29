using Microsoft.AspNetCore.Http;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Maintenance.Assignments.Assign;
using NorthernLink.Fleet.Application.Maintenance.Assignments.Unassign;
using NorthernLink.Fleet.Application.Maintenance.Completions.Log;
using NorthernLink.Fleet.Application.Maintenance.Plans.Create;
using NorthernLink.Fleet.Application.Maintenance.Plans.GetAll;
using NorthernLink.Fleet.Application.Maintenance.Plans.GetById;
using NorthernLink.Fleet.Application.Maintenance.Status.GetDue;
using NorthernLink.Fleet.Application.Maintenance.Status.GetFleetDue;
using NorthernLink.Fleet.Application.Maintenance.Status.GetHistory;
using NorthernLink.Fleet.Application.Maintenance.Status.GetOverhauls;
using NorthernLink.Fleet.Application.Maintenance.Status.GetVehicleStatus;
using NorthernLink.Fleet.Application.Maintenance.Plans.SeedDefaults;
using NorthernLink.Fleet.Application.Maintenance.Plans.Update;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Fleet.Infrastructure.Endpoints;

/// <summary>
/// Preventative-maintenance endpoints — plans fleet-wide under <c>/api/fleet/pm-plans</c>,
/// per-vehicle status/completions nested under the vehicle at <c>…/vehicles/{id}/pm</c>.
/// A vehicle with no plan answers the status-shaped GETs with <c>{ assigned: false }</c>
/// (200, never 404) so the frontend can render the assign call-to-action.
/// </summary>
public static partial class FleetEndpoints
{
    private static async Task<IResult> GetPmPlans(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetMaintenancePlansQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetPmPlanById(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetMaintenancePlanByIdQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreatePmPlan(
        PmPlanRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (FindPlanRequestBindingError(request) is { } bindingError)
        {
            return EndpointResults.Problem(bindingError);
        }

        var command = new CreateMaintenancePlanCommand(
            tenantId,
            request.Name ?? string.Empty,
            request.VehicleModel ?? string.Empty,
            request.ServiceClass ?? string.Empty,
            request.Notes,
            (request.Items ?? []).Select(ToItem).ToList(),
            (request.Overhauls ?? []).Select(ToOverhaul).ToList());

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/fleet/pm-plans/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdatePmPlan(
        Guid id, PmPlanRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (FindPlanRequestBindingError(request) is { } bindingError)
        {
            return EndpointResults.Problem(bindingError);
        }

        var command = new UpdateMaintenancePlanCommand(
            tenantId,
            id,
            request.Name ?? string.Empty,
            request.VehicleModel ?? string.Empty,
            request.ServiceClass ?? string.Empty,
            request.Notes,
            (request.Items ?? []).Select(ToItem).ToList(),
            (request.Overhauls ?? []).Select(ToOverhaul).ToList());

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> SeedDefaultPmPlan(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new SeedDefaultMaintenancePlanCommand(tenantId), cancellationToken);
        // Always 200: the command is idempotent and returns the same plan id whether this
        // run created the plan or found it already seeded — it does not surface which, so a
        // first-run 201 would need extra plumbing for no caller benefit.
        return result.IsSuccess
            ? Results.Ok(new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> AssignPmPlan(
        Guid vehicleId, AssignPmPlanRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        // Nullable so an omitted planId cannot silently bind to Guid.Empty and surface as a
        // misleading "plan not found" — the missing field is a 400, not a 404.
        if (request.PlanId is not { } planId)
        {
            return EndpointResults.Problem(MaintenanceErrors.PlanRequired);
        }

        var result = await sender.Send(
            new AssignMaintenancePlanCommand(tenantId, vehicleId, planId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UnassignPmPlan(
        Guid vehicleId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new UnassignMaintenancePlanCommand(tenantId, vehicleId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> LogPmCompletion(
        Guid vehicleId, PmCompletionRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        // Nullable so an omitted field cannot silently bind to 0 km — an explicit reading
        // is required (the same treatment PerformedAt gets via PerformedAtRequired).
        if (request.OdometerKm is not { } odometerKm)
        {
            return EndpointResults.Problem(MaintenanceErrors.CompletionOdometerRequired);
        }

        var command = new LogPmCompletionCommand(
            tenantId,
            vehicleId,
            request.Code ?? string.Empty,
            request.Kind,
            request.PerformedAt ?? default,
            odometerKm,
            request.PerformedBy ?? string.Empty,
            request.WorkOrderId,
            request.Measurement,
            request.Notes);

        var result = await sender.Send(command, cancellationToken);
        // Location points at the mapped history list route — there is no per-completion GET.
        return result.IsSuccess
            ? Results.Created($"/api/fleet/vehicles/{vehicleId}/pm/history", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetVehiclePmStatus(
        Guid vehicleId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetVehiclePmStatusQuery(tenantId, vehicleId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetVehiclePmDue(
        Guid vehicleId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetPmDueQuery(tenantId, vehicleId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetVehiclePmOverhauls(
        Guid vehicleId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetPmOverhaulsQuery(tenantId, vehicleId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetVehiclePmHistory(
        Guid vehicleId, int? limit, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(
            new GetPmHistoryQuery(tenantId, vehicleId, limit ?? IPmReadService.DefaultHistoryLimit),
            cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetFleetPmDue(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetFleetPmDueQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// The binding gaps JSON deserialization cannot reject on its own: Tier and Task are
    /// nullable on the request purely so an OMITTED field is caught here instead of
    /// silently binding to the first enum member (a wholesale PUT would otherwise rewrite
    /// every line's task to Inspect). Values present but out of range still fall through to
    /// the aggregate's Enum.IsDefined validation.
    /// </summary>
    private static Error? FindPlanRequestBindingError(PmPlanRequest request)
    {
        foreach (var item in request.Items ?? [])
        {
            if (item.Tier is null)
            {
                return MaintenanceErrors.ItemTierRequired;
            }

            if (item.Task is null)
            {
                return MaintenanceErrors.ItemTaskRequired;
            }
        }

        return null;
    }

    private static MaintenanceItem ToItem(PmPlanItemRequest request) => new()
    {
        Code = request.Code ?? string.Empty,
        System = request.System ?? string.Empty,
        Component = request.Component ?? string.Empty,
        // Null-forgiving is safe: FindPlanRequestBindingError runs first on every path.
        Tier = request.Tier!.Value,
        Task = request.Task!.Value,
        IntervalKm = request.IntervalKm,
        IntervalMonths = request.IntervalMonths,
        ShopMinutes = request.ShopMinutes,
        LeadKm = request.LeadKm,
        LeadDays = request.LeadDays,
        Notes = request.Notes,
    };

    private static OverhaulSpec ToOverhaul(PmPlanOverhaulRequest request) => new()
    {
        Code = request.Code ?? string.Empty,
        Component = request.Component ?? string.Empty,
        IntervalKm = request.IntervalKm,
        IntervalMonths = request.IntervalMonths,
        LabourHours = request.LabourHours,
        PartsCad = request.PartsCad,
        LeadKm = request.LeadKm,
        LeadDays = request.LeadDays,
        Scope = request.Scope ?? string.Empty,
        ConditionTriggers = [.. request.ConditionTriggers ?? []],
        RelatedItemCodes = [.. request.RelatedItemCodes ?? []],
    };
}

/// <summary>Request body for POST /api/fleet/pm-plans and PUT /api/fleet/pm-plans/{id} (wholesale).</summary>
public sealed record PmPlanRequest(
    string? Name,
    string? VehicleModel,
    string? ServiceClass,
    string? Notes,
    IReadOnlyList<PmPlanItemRequest>? Items,
    IReadOnlyList<PmPlanOverhaulRequest>? Overhauls);

/// <summary>
/// One routine maintenance line of a plan request. Null leads keep the defaults. Tier and
/// Task are nullable so an omitted field is rejected explicitly (TierRequired/TaskRequired)
/// instead of silently binding to the first enum member.
/// </summary>
public sealed record PmPlanItemRequest(
    string? Code,
    string? System,
    string? Component,
    ComponentTier? Tier,
    MaintenanceTask? Task,
    int? IntervalKm,
    int? IntervalMonths,
    int ShopMinutes,
    int? LeadKm,
    int? LeadDays,
    string? Notes);

/// <summary>One overhaul of a plan request. Null leads keep the defaults.</summary>
public sealed record PmPlanOverhaulRequest(
    string? Code,
    string? Component,
    int? IntervalKm,
    int? IntervalMonths,
    decimal LabourHours,
    decimal PartsCad,
    int? LeadKm,
    int? LeadDays,
    string? Scope,
    IReadOnlyList<string>? ConditionTriggers,
    IReadOnlyList<string>? RelatedItemCodes);

/// <summary>
/// Request body for POST /api/fleet/vehicles/{vehicleId}/pm/assign. PlanId is nullable so
/// an omitted field is a 400 PlanRequired instead of Guid.Empty masquerading as a 404.
/// </summary>
public sealed record AssignPmPlanRequest(Guid? PlanId);

/// <summary>
/// Request body for POST /api/fleet/vehicles/{vehicleId}/pm/completions. OdometerKm is
/// nullable so an omitted field is rejected explicitly instead of binding to 0 km.
/// </summary>
public sealed record PmCompletionRequest(
    string? Code,
    PmEntryKind Kind,
    DateOnly? PerformedAt,
    int? OdometerKm,
    string? PerformedBy,
    Guid? WorkOrderId,
    string? Measurement,
    string? Notes);
