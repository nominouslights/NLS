using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
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
        var vehicles = app.MapGroup("/api/fleet/vehicles");

        vehicles.MapGet("", GetVehicles);
        vehicles.MapGet("{id:guid}", GetVehicleById);
        vehicles.MapPost("", RegisterVehicle);
        vehicles.MapPut("{id:guid}", UpdateVehicle);
        vehicles.MapPost("{id:guid}/status", ChangeStatus);
        vehicles.MapPost("{id:guid}/odometer", RecordOdometer);
        vehicles.MapPost("{id:guid}/dispose", DisposeVehicle);
        vehicles.MapGet("{id:guid}/retirement-certificate", GetRetirementCertificate);

        return app;
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
