using Microsoft.AspNetCore.Http;
using NorthernLink.Fleet.Application.Documents.Add;
using NorthernLink.Fleet.Application.Documents.GetAll;
using NorthernLink.Fleet.Application.Documents.GetForVehicle;
using NorthernLink.Fleet.Application.Documents.Remove;
using NorthernLink.Fleet.Domain.Documents;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Fleet.Infrastructure.Endpoints;

/// <summary>Compliance document (metadata) endpoints — per-vehicle plus the fleet-wide compliance watch.</summary>
public static partial class FleetEndpoints
{
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
}

/// <summary>Request body for POST /api/fleet/vehicles/{vehicleId}/documents.</summary>
public sealed record DocumentRequest(
    DocumentType Type,
    string? FileName,
    int FileSizeKb,
    string? UploadedBy,
    DateTimeOffset? Expiry,
    string? Note);
