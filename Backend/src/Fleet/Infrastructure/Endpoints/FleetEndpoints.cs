using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Fleet.Application.Documents.Add;
using NorthernLink.Fleet.Application.Documents.GetAll;
using NorthernLink.Fleet.Application.Documents.GetForVehicle;
using NorthernLink.Fleet.Application.Documents.Remove;
using NorthernLink.Fleet.Domain.Documents;
using NorthernLink.Fleet.Application.Services.Add;
using NorthernLink.Fleet.Application.Services.GetForVehicle;
using NorthernLink.Fleet.Domain.Services;
using NorthernLink.Fleet.Application.WorkOrders.ChangeStatus;
using NorthernLink.Fleet.Application.WorkOrders.Complete;
using NorthernLink.Fleet.Application.WorkOrders.Create;
using NorthernLink.Fleet.Application.WorkOrders.GetAll;
using NorthernLink.Fleet.Application.WorkOrders.GetForVehicle;
using NorthernLink.Fleet.Domain.WorkOrders;
using NorthernLink.Fleet.Application.Inspections.Enter;
using NorthernLink.Fleet.Application.Inspections.GetInspections;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Application.Shops.GetShops;
using NorthernLink.Fleet.Application.Shops.Register;
using NorthernLink.Fleet.Application.Shops.Update;
using NorthernLink.Fleet.Application.Vehicles.ChangeStatus;
using NorthernLink.Fleet.Application.Vehicles.Dispose;
using NorthernLink.Fleet.Application.Vehicles.GetRetirementCertificate;
using NorthernLink.Fleet.Application.Vehicles.GetVehicleById;
using NorthernLink.Fleet.Application.Vehicles.GetVehicles;
using NorthernLink.Fleet.Application.Vehicles.RecordOdometer;
using NorthernLink.Fleet.Application.Vehicles.Register;
using NorthernLink.Fleet.Application.Vehicles.Update;
using NorthernLink.Fleet.Domain.Vehicles;

namespace NorthernLink.Fleet.Infrastructure.Endpoints;

/// <summary>
/// The Fleet module's minimal-API surface under <c>/api/fleet/vehicles</c>. Every
/// endpoint resolves the ambient tenant (401 when absent — the API half of dual tenant
/// enforcement), stamps it onto the command/query, and dispatches via <see cref="ISender"/>.
/// </summary>
public static class FleetEndpoints
{
    public static IEndpointRouteBuilder MapFleetEndpoints(this IEndpointRouteBuilder app)
    {
        var vehicles = app.MapGroup("/api/fleet/vehicles").RequireAuthorization();

        vehicles.MapGet("", GetVehicles);
        vehicles.MapGet("{id:guid}", GetVehicleById);
        vehicles.MapPost("", RegisterVehicle);
        vehicles.MapPut("{id:guid}", UpdateVehicle);
        vehicles.MapPost("{id:guid}/status", ChangeStatus);
        vehicles.MapPost("{id:guid}/odometer", RecordOdometer);
        vehicles.MapPost("{id:guid}/dispose", DisposeVehicle);
        vehicles.MapGet("{id:guid}/retirement-certificate", GetRetirementCertificate);

        // Compliance documents (metadata) — nested under their vehicle.
        vehicles.MapGet("{vehicleId:guid}/documents", GetVehicleDocuments);
        vehicles.MapPost("{vehicleId:guid}/documents", AddVehicleDocument);
        vehicles.MapDelete("{vehicleId:guid}/documents/{documentId:guid}", RemoveVehicleDocument);

        // Service history (NSC Standard 13) — nested under their vehicle.
        vehicles.MapGet("{vehicleId:guid}/service-records", GetVehicleServiceRecords);
        vehicles.MapPost("{vehicleId:guid}/service-records", AddServiceRecord);

        // Work orders — per-vehicle listing plus a fleet-wide group.
        vehicles.MapGet("{vehicleId:guid}/work-orders", GetVehicleWorkOrders);

        var workOrders = app.MapGroup("/api/fleet/work-orders");
        workOrders.MapGet("", GetAllWorkOrders);
        workOrders.MapPost("", CreateWorkOrder);
        workOrders.MapPost("{id:guid}/status", ChangeWorkOrderStatus);
        workOrders.MapPost("{id:guid}/complete", CompleteWorkOrder);

        // Inspections are read-only here: rows are materialized by the
        // trips.trip-manifest-completed event consumer, never posted directly.
        var inspections = app.MapGroup("/api/fleet/inspections");

        inspections.MapGet("", GetInspections);
        inspections.MapPost("", EnterInspection);

        // Fleet-wide compliance documents (dashboard compliance watch).
        app.MapGet("/api/fleet/documents", GetAllDocuments);

        // Shops / parts partners — fleet-wide reference data reused on work orders.
        var shops = app.MapGroup("/api/fleet/shops");

        shops.MapGet("", GetShops);
        shops.MapPost("", RegisterShop);
        shops.MapPut("{id:guid}", UpdateShop);

        return app;
    }

    private static async Task<IResult> GetVehicleDocuments(
        Guid vehicleId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetVehicleDocumentsQuery(tenantId, vehicleId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetAllDocuments(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetAllDocumentsQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> AddVehicleDocument(
        Guid vehicleId, DocumentRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new AddVehicleDocumentCommand(
            tenantId,
            vehicleId,
            request.Type,
            request.FileName ?? string.Empty,
            request.FileSizeKb,
            request.UploadedBy ?? "Dispatch",
            request.Expiry,
            request.Note);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/fleet/vehicles/{vehicleId}/documents/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RemoveVehicleDocument(
        Guid vehicleId, Guid documentId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new RemoveVehicleDocumentCommand(tenantId, documentId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

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

    private static async Task<IResult> GetShops(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetShopsQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RegisterShop(
        ShopRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new RegisterShopCommand(
            tenantId,
            request.Name ?? string.Empty,
            request.ContactName,
            request.Phone,
            request.Email,
            request.Address,
            request.GstBusinessNo,
            request.MpiAccredited,
            request.InspectionStationNo,
            request.SuppliesParts,
            request.Notes);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/fleet/shops/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateShop(
        Guid id, ShopRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateShopCommand(
            tenantId,
            id,
            request.Name ?? string.Empty,
            request.ContactName,
            request.Phone,
            request.Email,
            request.Address,
            request.GstBusinessNo,
            request.MpiAccredited,
            request.InspectionStationNo,
            request.SuppliesParts,
            request.Notes);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

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

    private static async Task<IResult> GetVehicles(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetVehiclesQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetVehicleById(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetVehicleByIdQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RegisterVehicle(
        RegisterVehicleRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new RegisterVehicleCommand(
            tenantId,
            request.UnitNumber ?? string.Empty,
            request.Vin ?? string.Empty,
            request.Make ?? string.Empty,
            request.Model ?? string.Empty,
            request.Year,
            request.SeatingCapacity,
            request.LicencePlate ?? string.Empty,
            request.RequiredLicenceClass ?? string.Empty,
            request.OdometerKm,
            request.AcquisitionCostCad,
            request.EndOfLifeKm);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/fleet/vehicles/{result.Value}", new VehicleCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateVehicle(
        Guid id, UpdateVehicleRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateVehicleCommand(
            tenantId,
            id,
            request.UnitNumber ?? string.Empty,
            request.Vin ?? string.Empty,
            request.Make ?? string.Empty,
            request.Model ?? string.Empty,
            request.Year,
            request.SeatingCapacity,
            request.LicencePlate ?? string.Empty,
            request.RequiredLicenceClass ?? string.Empty,
            request.AcquisitionCostCad,
            request.EndOfLifeKm);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ChangeStatus(
        Guid id, ChangeVehicleStatusRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new ChangeVehicleStatusCommand(tenantId, id, request.Status, request.Reason),
            cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RecordOdometer(
        Guid id, RecordOdometerRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new RecordOdometerCommand(tenantId, id, request.OdometerKm),
            cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> DisposeVehicle(
        Guid id, DisposeVehicleRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new DisposeVehicleCommand(tenantId, id, request.Method, request.SalePriceCad, request.Note),
            cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetRetirementCertificate(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetRetirementCertificateQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }
}

/// <summary>Body of a successful vehicle registration (201, with Location header).</summary>
public sealed record VehicleCreatedResponse(Guid Id);

/// <summary>Body of a successful create (201, with Location header) for maintenance entities.</summary>
public sealed record EntityCreatedResponse(Guid Id);

/// <summary>Request body for POST /api/fleet/vehicles/{vehicleId}/documents.</summary>
public sealed record DocumentRequest(
    DocumentType Type,
    string? FileName,
    int FileSizeKb,
    string? UploadedBy,
    DateTimeOffset? Expiry,
    string? Note);

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

/// <summary>Request body for POST/PUT /api/fleet/shops.</summary>
public sealed record ShopRequest(
    string? Name,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    string? GstBusinessNo,
    bool MpiAccredited,
    string? InspectionStationNo,
    bool SuppliesParts,
    string? Notes);

/// <summary>Request body for POST /api/fleet/vehicles.</summary>
public sealed record RegisterVehicleRequest(
    string? UnitNumber,
    string? Vin,
    string? Make,
    string? Model,
    int Year,
    int SeatingCapacity,
    string? LicencePlate,
    string? RequiredLicenceClass,
    int OdometerKm,
    decimal AcquisitionCostCad,
    int EndOfLifeKm);

/// <summary>Request body for PUT /api/fleet/vehicles/{id}.</summary>
public sealed record UpdateVehicleRequest(
    string? UnitNumber,
    string? Vin,
    string? Make,
    string? Model,
    int Year,
    int SeatingCapacity,
    string? LicencePlate,
    string? RequiredLicenceClass,
    decimal AcquisitionCostCad,
    int EndOfLifeKm);

/// <summary>Request body for POST /api/fleet/vehicles/{id}/status. Status is the enum name, e.g. "InMaintenance".</summary>
public sealed record ChangeVehicleStatusRequest(VehicleStatus Status, string? Reason);

/// <summary>Request body for POST /api/fleet/vehicles/{id}/odometer.</summary>
public sealed record RecordOdometerRequest(int OdometerKm);

/// <summary>Request body for POST /api/fleet/vehicles/{id}/dispose. Method is "Sold" or "Recycled".</summary>
public sealed record DisposeVehicleRequest(DisposalMethod Method, decimal? SalePriceCad, string? Note);
