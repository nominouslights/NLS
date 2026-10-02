using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Application.BookeoImports;
using NorthernLink.Trips.Application.BookeoImports.Commit;
using NorthernLink.Trips.Application.BookeoImports.Mappings;
using NorthernLink.Trips.Application.BookeoImports.Preview;
using NorthernLink.Trips.Domain.BookeoImports;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Infrastructure.Endpoints;

/// <summary>
/// The Bookeo booking-report import — <c>/api/trips/imports/bookeo</c>, dispatch work end to end
/// (DispatchAccess on the group). Upload → preview (nothing written but the batch row) → commit
/// with the preview's plan hash; plus the product/unit mappings the preview asks for and the upload
/// history. Called from <see cref="TripsEndpoints"/>. Like every Trips endpoint: the ambient tenant
/// is resolved (401 when absent) and stamped onto the command; the actor's name for the audit
/// stamps comes from the signed token (<see cref="ICurrentActor"/>), never from the request.
/// </summary>
internal static class BookeoImportEndpoints
{
    /// <summary>Headroom over the 5 MB file limit for the multipart envelope itself.</summary>
    private const long MaxRequestBytes = BookeoImportErrors.MaxFileBytes + (64 * 1024);

    public static void MapBookeoImportEndpoints(this IEndpointRouteBuilder app)
    {
        var imports = app.MapGroup("/api/trips/imports/bookeo")
            .RequireAuthorization(AuthorizationPolicies.DispatchAccess);

        // Multipart upload, same shape as the Drivers credential-image endpoint: no antiforgery
        // (bearer-token API, no cookies), size checked before the body is buffered.
        imports.MapPost("preview", Preview).DisableAntiforgery();
        imports.MapPost("{batchId:guid}/commit", Commit);

        imports.MapGet("product-mappings", GetProductMappings);
        imports.MapPut("product-mappings", UpsertProductMappings);
        imports.MapDelete("product-mappings/{id:guid}", DeleteProductMapping);

        imports.MapGet("unit-mappings", GetUnitMappings);
        imports.MapPut("unit-mappings", UpsertUnitMappings);
        imports.MapDelete("unit-mappings/{id:guid}", DeleteUnitMapping);

        imports.MapGet("batches", GetBatches); // ?take=20
    }

    private static async Task<IResult> Preview(
        HttpRequest request,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (request.ContentLength > MaxRequestBytes)
        {
            return EndpointResults.Problem(BookeoImportErrors.FileTooLarge);
        }

        if (!request.HasFormContentType)
        {
            return EndpointResults.Problem(BookeoImportErrors.FileRequired);
        }

        IFormFile? file;
        try
        {
            var form = await request.ReadFormAsync(cancellationToken);
            file = form.Files.GetFile("file");
        }
        catch (Exception exception) when (exception is InvalidDataException or BadHttpRequestException)
        {
            // The multipart reader's own limits (or a truncated body) — to the caller, a too-big upload.
            return EndpointResults.Problem(BookeoImportErrors.FileTooLarge);
        }

        if (file is null || file.Length == 0)
        {
            return EndpointResults.Problem(BookeoImportErrors.FileRequired);
        }

        if (file.Length > BookeoImportErrors.MaxFileBytes)
        {
            return EndpointResults.Problem(BookeoImportErrors.FileTooLarge);
        }

        byte[] content;
        await using (var stream = file.OpenReadStream())
        using (var buffer = new MemoryStream((int)file.Length))
        {
            await stream.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }

        var result = await sender.Send(
            new PreviewBookeoImportCommand(tenantId, file.FileName, content, ActorName(currentActor)),
            cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> Commit(
        Guid batchId,
        CommitBookeoImportRequest request,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new CommitBookeoImportCommand(tenantId, batchId, request.PlanHash, ActorName(currentActor)),
            cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetProductMappings(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetBookeoProductMappingsQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpsertProductMappings(
        UpsertBookeoProductMappingsRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var inputs = (request.Mappings ?? [])
            .Select(m => new BookeoProductMappingInput(
                m.ProductCode, m.ProductName, m.Destination, m.RouteId, m.Direction, m.ResidentStopRole ?? ResidentStopRole.Pickup))
            .ToList();

        var result = await sender.Send(new UpsertBookeoProductMappingsCommand(tenantId, inputs), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> DeleteProductMapping(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new DeleteBookeoProductMappingCommand(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetUnitMappings(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetBookeoUnitMappingsQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpsertUnitMappings(
        UpsertBookeoUnitMappingsRequest request,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var inputs = (request.Mappings ?? [])
            .Select(m => new BookeoUnitMappingInput(m.UnitText, m.VehicleId))
            .ToList();

        var result = await sender.Send(new UpsertBookeoUnitMappingsCommand(tenantId, inputs), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> DeleteUnitMapping(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new DeleteBookeoUnitMappingCommand(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetBatches(
        int? take, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(
            new GetBookeoImportBatchesQuery(tenantId, take ?? GetBookeoImportBatchesQueryHandler.DefaultTake),
            cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>The token's email, else its user id — who uploaded/committed, for the history and the manifest stamp.</summary>
    private static string ActorName(ICurrentActor actor) =>
        actor.Email ?? actor.UserId?.ToString() ?? "unknown";
}

/// <summary>Body of POST /api/trips/imports/bookeo/{batchId}/commit — the preview's <c>planHash</c>.</summary>
public sealed record CommitBookeoImportRequest(string? PlanHash);

/// <summary>
/// Body of PUT /api/trips/imports/bookeo/product-mappings. <c>direction</c> is a
/// <c>TripDirection</c> name ("Inbound" | "Outbound") or null; <c>residentStopRole</c> is
/// "Pickup" | "Dropoff". Upserted by (productCode, destination); the response is the full list.
/// </summary>
public sealed record UpsertBookeoProductMappingsRequest(IReadOnlyList<BookeoProductMappingRequest>? Mappings);

public sealed record BookeoProductMappingRequest(
    string? ProductCode,
    string? ProductName,
    string? Destination,
    Guid RouteId,
    TripDirection? Direction,
    ResidentStopRole? ResidentStopRole);

/// <summary>Body of PUT /api/trips/imports/bookeo/unit-mappings. Upserted by unit text; the response is the full list.</summary>
public sealed record UpsertBookeoUnitMappingsRequest(IReadOnlyList<BookeoUnitMappingRequest>? Mappings);

public sealed record BookeoUnitMappingRequest(string? UnitText, Guid VehicleId);
