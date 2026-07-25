using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace NorthernLink.Fleet.Infrastructure.Endpoints;

/// <summary>
/// The Fleet module's minimal-API surface under <c>/api/fleet</c>. Every endpoint resolves
/// the ambient tenant (401 when absent — the API half of dual tenant enforcement), stamps
/// it onto the command/query, and dispatches via <c>ISender</c>. This file owns the route
/// table; the handlers live in the sibling partial files, one per resource
/// (FleetEndpoints.Vehicles.cs, .Inspections.cs, .Shops.cs, .Documents.cs,
/// .ServiceRecords.cs, .WorkOrders.cs).
/// </summary>
public static partial class FleetEndpoints
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
}

/// <summary>Body of a successful vehicle registration (201, with Location header).</summary>
public sealed record VehicleCreatedResponse(Guid Id);

/// <summary>Body of a successful create (201, with Location header) for maintenance entities.</summary>
public sealed record EntityCreatedResponse(Guid Id);
