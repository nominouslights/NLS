using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Kernel;

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
        // DispatchAccess throughout except the DVIR submit below. Vehicles carry registration,
        // insurance, disposal and odometer writes; a bare RequireAuthorization let a Driver token
        // reach all of them. Not listed in the approved plan's narrow-list, but narrowing is the
        // conservative direction: the Driver Field App's vehicle needs are served by the
        // inspection POST, and anything more gets its own sibling group when it is specified.
        var vehicles = app.MapGroup("/api/fleet/vehicles")
            .RequireAuthorization(AuthorizationPolicies.DispatchAccess);

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

        // Defects on this vehicle, derived from its DVIRs. ?includeResolved=true for the full
        // history. Nested under the vehicle even though the contract is an inspection DTO —
        // the dispatcher asks "what is wrong with this truck", not "what did this DVIR say".
        vehicles.MapGet("{vehicleId:guid}/defects", GetVehicleDefects);

        // Preventative maintenance, per vehicle: computed status/due/overhauls/history,
        // plan assignment, and the append-only completion log.
        vehicles.MapGet("{vehicleId:guid}/pm", GetVehiclePmStatus);
        vehicles.MapPost("{vehicleId:guid}/pm/assign", AssignPmPlan);
        vehicles.MapDelete("{vehicleId:guid}/pm", UnassignPmPlan);
        vehicles.MapPost("{vehicleId:guid}/pm/completions", LogPmCompletion);
        vehicles.MapGet("{vehicleId:guid}/pm/due", GetVehiclePmDue);
        vehicles.MapGet("{vehicleId:guid}/pm/overhauls", GetVehiclePmOverhauls);
        vehicles.MapGet("{vehicleId:guid}/pm/history", GetVehiclePmHistory);

        // Fleet-wide PM dashboard: every assigned, non-disposed vehicle with its due picture.
        app.MapGet("/api/fleet/pm/due", GetFleetPmDue)
            .RequireAuthorization(AuthorizationPolicies.DispatchAccess);

        // Preventative-maintenance plans — fleet-wide reference data assigned to vehicles.
        var pmPlans = app.MapGroup("/api/fleet/pm-plans")
            .RequireAuthorization(AuthorizationPolicies.DispatchAccess);

        pmPlans.MapGet("", GetPmPlans);
        pmPlans.MapGet("{id:guid}", GetPmPlanById);
        pmPlans.MapPost("", CreatePmPlan);
        pmPlans.MapPut("{id:guid}", UpdatePmPlan);
        // Idempotent: installs the default Transit T-150 severe-service plan (and assigns
        // NL-01 when unassigned); reruns return the same plan id with no changes.
        pmPlans.MapPost("seed-defaults", SeedDefaultPmPlan);

        var workOrders = app.MapGroup("/api/fleet/work-orders")
            .RequireAuthorization(AuthorizationPolicies.DispatchAccess);
        workOrders.MapGet("", GetAllWorkOrders);
        workOrders.MapPost("", CreateWorkOrder);
        workOrders.MapPost("{id:guid}/status", ChangeWorkOrderStatus);
        workOrders.MapPost("{id:guid}/complete", CompleteWorkOrder);

        // Inspections, split across two sibling groups on one prefix. Listing every DVIR in the
        // fleet, and correcting or deleting one after the fact, is a compliance-office job…
        var inspectionRecords = app.MapGroup("/api/fleet/inspections")
            .RequireAuthorization(AuthorizationPolicies.DispatchAccess);

        inspectionRecords.MapGet("", GetInspections);
        inspectionRecords.MapPut("{id:guid}", UpdateInspection);
        inspectionRecords.MapDelete("{id:guid}", RemoveInspection);

        // Clear one defect. The item goes in the BODY, not the path — it is free text, and
        // (inspection id, item) is the only address a defect has. This sits with the records
        // group, not the submission one: clearing a defect is the compliance office answering a
        // DVIR, not part of the driver's own circle check.
        inspectionRecords.MapPost("{id:guid}/defects/resolve", ResolveInspectionDefect);

        // …while submitting a pre-/post-trip inspection is the driver's own circle check, and is
        // the reason DriverAccess exists. Entered from the trip workflow today, from the Driver
        // Field App next; the reading advances the vehicle odometer intra-Fleet.
        //
        // No caller-owns-this-row check here, and none is needed: the route carries no
        // {driverId}, an inspection is append-only, and the driver on it is a name the submitter
        // supplies. A driver submitting a DVIR under someone else's name is a falsified record —
        // a supervision problem, not something an ownership guard on a URL segment can catch.
        var inspectionSubmission = app.MapGroup("/api/fleet/inspections")
            .RequireAuthorization(AuthorizationPolicies.DriverAccess);

        inspectionSubmission.MapPost("", EnterInspection);

        // Fleet-wide compliance documents (dashboard compliance watch).
        app.MapGet("/api/fleet/documents", GetAllDocuments)
            .RequireAuthorization(AuthorizationPolicies.DispatchAccess);

        // Shops / parts partners — fleet-wide reference data reused on work orders.
        var shops = app.MapGroup("/api/fleet/shops")
            .RequireAuthorization(AuthorizationPolicies.DispatchAccess);

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
