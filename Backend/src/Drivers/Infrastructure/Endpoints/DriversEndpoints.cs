using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Clearances.GetForDriver;
using NorthernLink.Drivers.Application.Clearances.Grant;
using NorthernLink.Drivers.Application.Clearances.Revoke;
using NorthernLink.Drivers.Application.Credentials.Add;
using NorthernLink.Drivers.Application.Credentials.GetForDriver;
using NorthernLink.Drivers.Application.Credentials.Remove;
using NorthernLink.Drivers.Application.Credentials.SetImage;
using NorthernLink.Drivers.Application.Drivers.ChangeStatus;
using NorthernLink.Drivers.Application.Drivers.GetDriverById;
using NorthernLink.Drivers.Application.Drivers.GetDrivers;
using NorthernLink.Drivers.Application.Drivers.Register;
using NorthernLink.Drivers.Application.Drivers.Update;
using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Application.Hos.GetForDriver;
using NorthernLink.Drivers.Application.Hos.Record;
using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Storage;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Infrastructure.Endpoints;

/// <summary>
/// The Drivers module's minimal-API surface under <c>/api/drivers</c>. Every endpoint
/// resolves the ambient tenant (401 when absent — the API half of dual tenant
/// enforcement), stamps it onto the command/query, and dispatches via <see cref="ISender"/>.
/// </summary>
public static class DriversEndpoints
{
    public static IEndpointRouteBuilder MapDriversEndpoints(this IEndpointRouteBuilder app)
    {
        var drivers = app.MapGroup("/api/drivers").RequireAuthorization();

        drivers.MapGet("", GetDrivers);
        drivers.MapGet("{id:guid}", GetDriverById);
        drivers.MapPost("", RegisterDriver);
        drivers.MapPut("{id:guid}", UpdateDriver);
        drivers.MapPost("{id:guid}/status", ChangeStatus);

        // Compliance credentials — nested under their driver.
        drivers.MapGet("{driverId:guid}/credentials", GetDriverCredentials);
        drivers.MapPost("{driverId:guid}/credentials", AddDriverCredential);
        drivers.MapDelete("{driverId:guid}/credentials/{credentialId:guid}", RemoveDriverCredential);
        drivers.MapPost("{driverId:guid}/credentials/{credentialId:guid}/image", SetCredentialImage)
            .DisableAntiforgery();
        drivers.MapGet("{driverId:guid}/credentials/{credentialId:guid}/image", GetCredentialImage);

        // Client-site clearances — nested under their driver.
        drivers.MapGet("{driverId:guid}/clearances", GetDriverClearances);
        drivers.MapPost("{driverId:guid}/clearances", GrantDriverClearance);
        drivers.MapDelete("{driverId:guid}/clearances/{clearanceId:guid}", RevokeDriverClearance);

        // Hours of Service duty logs — nested under their driver.
        drivers.MapGet("{driverId:guid}/hos", GetDriverHosEntries);
        drivers.MapPost("{driverId:guid}/hos", RecordDriverHosEntry);

        return app;
    }

    private static async Task<IResult> GetDrivers(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetDriversQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetDriverById(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetDriverByIdQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RegisterDriver(
        RegisterDriverRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new RegisterDriverCommand(
            tenantId,
            request.Name ?? string.Empty,
            request.Phone,
            request.LicenceClass ?? string.Empty,
            request.LicenceExpiry,
            request.Source ?? string.Empty,
            request.HasWorkPermit);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/drivers/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateDriver(
        Guid id, UpdateDriverRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new UpdateDriverCommand(
            tenantId,
            id,
            request.Name ?? string.Empty,
            request.Phone,
            request.LicenceClass ?? string.Empty,
            request.LicenceExpiry,
            request.Source ?? string.Empty,
            request.HasWorkPermit);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ChangeStatus(
        Guid id, ChangeDriverStatusRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new ChangeDriverStatusCommand(tenantId, id, request.Status),
            cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetDriverCredentials(
        Guid driverId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetDriverCredentialsQuery(tenantId, driverId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> AddDriverCredential(
        Guid driverId, DriverCredentialRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new AddDriverCredentialCommand(
            tenantId,
            driverId,
            request.Type ?? string.Empty,
            request.Label ?? string.Empty,
            request.Issued ?? DateOnly.FromDateTime(DateTime.UtcNow),
            request.Expiry,
            request.Optional,
            request.Note);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/drivers/{driverId}/credentials/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RemoveDriverCredential(
        Guid driverId, Guid credentialId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new RemoveDriverCredentialCommand(tenantId, credentialId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetDriverClearances(
        Guid driverId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetDriverClearancesQuery(tenantId, driverId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GrantDriverClearance(
        Guid driverId, DriverClearanceRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new GrantDriverClearanceCommand(
            tenantId,
            driverId,
            request.Title ?? string.Empty,
            request.ClientName ?? string.Empty,
            request.Expiry);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/drivers/{driverId}/clearances/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RevokeDriverClearance(
        Guid driverId, Guid clearanceId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new RevokeDriverClearanceCommand(tenantId, clearanceId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetDriverHosEntries(
        Guid driverId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetHosEntriesQuery(tenantId, driverId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> SetCredentialImage(
        Guid driverId, Guid credentialId, IFormFile image, ITenantContext tenantContext, ISender sender, IDriverCredentialRepository credentialRepo, IObjectStorage objectStorage, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        const long MaxSizeBytes = 8 * 1024 * 1024; // 8 MB
        if (image.Length > MaxSizeBytes)
        {
            return EndpointResults.Problem(Error.Validation(
                "Drivers.Credential.ImageTooLarge",
                $"Image must be smaller than 8 MB (was {image.Length / 1024 / 1024} MB)."));
        }

        var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/heic" };
        if (!allowedContentTypes.Contains(image.ContentType))
        {
            return EndpointResults.Problem(Error.Validation(
                "Drivers.Credential.ImageInvalidType",
                $"Only JPEG, PNG, and HEIC images are allowed (was {image.ContentType})."));
        }

        // Query the write model (repository) directly instead of the read model to avoid
        // eventual-consistency lags. The credential was just created and exists immediately.
        var credential = await credentialRepo.GetByIdAsync(credentialId, cancellationToken);
        if (credential is null || credential.DriverId != driverId)
        {
            return Results.NotFound();
        }

        // Deterministic key: driver-credentials/{tenantId}/{driverId}/{credentialId}/original{ext}
        var ext = Path.GetExtension(image.FileName) ?? ".bin";
        var imageKey = $"driver-credentials/{tenantId}/{driverId}/{credentialId}/original{ext}";

        try
        {
            await using var stream = image.OpenReadStream();
            await objectStorage.PutAsync(imageKey, stream, image.ContentType, cancellationToken);
        }
        catch (Exception ex)
        {
            return EndpointResults.Problem(Error.Validation(
                "Drivers.Credential.ImageUploadFailed",
                $"Failed to upload image: {ex.Message}"));
        }

        var setImageResult = await sender.Send(
            new SetDriverCredentialImageCommand(tenantId, credentialId, driverId, imageKey, image.ContentType),
            cancellationToken);

        return setImageResult.IsSuccess ? Results.NoContent() : EndpointResults.Problem(setImageResult.Error);
    }

    private static async Task<IResult> GetCredentialImage(
        Guid driverId, Guid credentialId, ITenantContext tenantContext, IDriverCredentialRepository credentialRepo, IObjectStorage objectStorage, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        // Same write-model lookup as SetCredentialImage — an image attached moments ago must be
        // fetchable immediately, without waiting on the read-model projection.
        var credential = await credentialRepo.GetByIdAsync(credentialId, cancellationToken);
        if (credential is null || credential.DriverId != driverId || credential.ImageKey is null)
        {
            return Results.NotFound();
        }

        try
        {
            var stream = await objectStorage.GetAsync(credential.ImageKey, cancellationToken);
            return Results.File(stream, credential.ImageContentType, enableRangeProcessing: true);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (Exception ex)
        {
            return EndpointResults.Problem(Error.Validation(
                "Drivers.Credential.ImageFetchFailed",
                $"Failed to fetch image: {ex.Message}"));
        }
    }

    private static async Task<IResult> RecordDriverHosEntry(
        Guid driverId, HosEntryRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        // The friendly duty string ("Off Duty"/"On Duty"/"Driving") maps to the domain enum here;
        // an unrecognized value is a validation problem, not a 500.
        var dutyResult = HosDisplay.DutyFromWire(request.Duty);
        if (dutyResult.IsFailure)
        {
            return EndpointResults.Problem(dutyResult.Error);
        }

        // Source is fixed server-side to ManualPaperBackup — a dispatcher console entry is a
        // paper backup by definition; the request carries no source field.
        var command = new RecordHosEntryCommand(
            tenantId,
            driverId,
            request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow),
            dutyResult.Value,
            request.OnDutyH,
            request.DrivingH,
            request.OffDutyH,
            request.EnteredBy,
            request.Note);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/drivers/{driverId}/hos/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }
}

/// <summary>Body of a successful create (201, with Location header).</summary>
public sealed record EntityCreatedResponse(Guid Id);

/// <summary>Request body for POST /api/drivers.</summary>
public sealed record RegisterDriverRequest(
    string? Name,
    string? Phone,
    string? LicenceClass,
    DateOnly? LicenceExpiry,
    string? Source,
    bool HasWorkPermit);

/// <summary>Request body for PUT /api/drivers/{id}.</summary>
public sealed record UpdateDriverRequest(
    string? Name,
    string? Phone,
    string? LicenceClass,
    DateOnly? LicenceExpiry,
    string? Source,
    bool HasWorkPermit);

/// <summary>Request body for POST /api/drivers/{id}/status. Status is the enum name, e.g. "Inactive".</summary>
public sealed record ChangeDriverStatusRequest(DriverStatus Status);

/// <summary>Request body for POST /api/drivers/{driverId}/credentials. Issued defaults to today (UTC).</summary>
public sealed record DriverCredentialRequest(
    string? Type,
    string? Label,
    DateOnly? Issued,
    DateOnly? Expiry,
    bool Optional,
    string? Note);

/// <summary>Request body for POST /api/drivers/{driverId}/clearances.</summary>
public sealed record DriverClearanceRequest(
    string? Title,
    string? ClientName,
    DateOnly? Expiry);

/// <summary>
/// Request body for POST /api/drivers/{driverId}/hos. <see cref="Duty"/> is the friendly
/// string ("Off Duty"/"On Duty"/"Driving"). There is deliberately no source field — the
/// endpoint fixes source to Manual (paper backup). <see cref="Date"/> defaults to today
/// (UTC). <see cref="EnteredBy"/> is the dispatcher's name (required for the manual path).
/// </summary>
public sealed record HosEntryRequest(
    DateOnly? Date,
    string? Duty,
    decimal OnDutyH,
    decimal DrivingH,
    decimal OffDutyH,
    string? EnteredBy,
    string? Note);
