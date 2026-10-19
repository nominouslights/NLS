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
using NorthernLink.Drivers.Application.Drivers.GetByUser;
using NorthernLink.Drivers.Application.Drivers.GetDriverById;
using NorthernLink.Drivers.Application.Drivers.GetDrivers;
using NorthernLink.Drivers.Application.Drivers.LinkUser;
using NorthernLink.Drivers.Application.Drivers.Register;
using NorthernLink.Drivers.Application.Drivers.SelfAccess;
using NorthernLink.Drivers.Application.Drivers.SetMotiveUser;
using NorthernLink.Drivers.Application.Drivers.UnlinkUser;
using NorthernLink.Drivers.Application.Drivers.Update;
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
        // FOUR groups on the same "/api/drivers" prefix, not one. ASP.NET group policies are
        // ADDITIVE — a nested group's policy ANDs with its parent's — so there is no way to
        // widen access from inside a narrowed group. Several MapGroup calls on one prefix is
        // legal; routes stay unambiguous because they are distinguished by template and method.
        //
        // Roster administration: registering drivers, editing them, granting and revoking
        // credentials and clearances, and linking a driver to a login account.
        var driverAdmin = app.MapGroup("/api/drivers")
            .RequireAuthorization(AuthorizationPolicies.DispatchAccess);

        driverAdmin.MapPost("", RegisterDriver);
        driverAdmin.MapPut("{id:guid}", UpdateDriver);
        driverAdmin.MapPost("{id:guid}/status", ChangeStatus);

        // Identity link — who may sign into the Driver Field App as this driver. Dispatch-only,
        // and deliberately its own pair of routes rather than a field on the roster form: it is
        // an access grant, not a roster detail.
        driverAdmin.MapPost("{id:guid}/user", LinkDriverUser);
        driverAdmin.MapDelete("{id:guid}/user", UnlinkDriverUser);

        // Motive (ELD) mapping — whose Motive duty-status logs the HOS sync will mirror onto this
        // driver's ledger. Roster administration, so dispatch-only like the Identity link, and its
        // own pair of routes rather than a field on the roster form.
        driverAdmin.MapPut("{id:guid}/motive", SetDriverMotiveUserId);
        driverAdmin.MapDelete("{id:guid}/motive", ClearDriverMotiveUserId);

        // Compliance credentials — nested under their driver. Writes are dispatch-only.
        driverAdmin.MapPost("{driverId:guid}/credentials", AddDriverCredential);
        driverAdmin.MapDelete("{driverId:guid}/credentials/{credentialId:guid}", RemoveDriverCredential);
        driverAdmin.MapPost("{driverId:guid}/credentials/{credentialId:guid}/image", SetCredentialImage)
            .DisableAntiforgery();
        driverAdmin.MapGet("{driverId:guid}/credentials/{credentialId:guid}/image", GetCredentialImage);

        // Client-site clearances — nested under their driver. Granting and revoking is dispatch.
        driverAdmin.MapPost("{driverId:guid}/clearances", GrantDriverClearance);
        driverAdmin.MapDelete("{driverId:guid}/clearances/{clearanceId:guid}", RevokeDriverClearance);

        // The roster's READ routes: DispatchAccess widened with ComplianceOfficer, so the
        // console's Drivers screen (and the roster HOS batch and today's compliance alerts)
        // work for a compliance officer without the write routes above ever admitting one.
        // "hos/…" cannot collide with "{id:guid}": "hos" is not a Guid.
        var roster = app.MapGroup("/api/drivers")
            .RequireAuthorization(AuthorizationPolicies.DriverRosterAccess);

        roster.MapGet("", GetDrivers);
        roster.MapGet("{id:guid}", GetDriverById);
        roster.MapGet("hos/status", HosEndpoints.GetRosterHosStatus);
        roster.MapGet("hos/alerts/today", HosEndpoints.GetTodayAlerts);

        // The Driver Field App's own surface. DriverAccess admits every Driver, so it is a
        // route-level gate ONLY: each {driverId} route below additionally runs the
        // caller-owns-this-row check (IDriverSelfAccess) before it does anything, or driver A
        // could read driver B's credentials.
        var driverSelf = app.MapGroup("/api/drivers")
            .RequireAuthorization(AuthorizationPolicies.DriverAccess);

        // "me" is the Field App's first call. No {driverId} and so no ownership check needed —
        // the id comes from the token's sub, so there is nothing for a caller to tamper with.
        // The literal "me" cannot collide with "{id:guid}" above: it is not a Guid.
        driverSelf.MapGet("me", GetMyDriverRecord);

        driverSelf.MapGet("{driverId:guid}/credentials", GetDriverCredentials);
        driverSelf.MapGet("{driverId:guid}/clearances", GetDriverClearances);

        // Hours of service — the fourth sibling group (HosAccess), mapped in HosEndpoints.
        HosEndpoints.Map(app);

        // Compliance decisions — the fifth sibling group (ComplianceAccess), mapped in
        // ComplianceEndpoints: deferral approval, out-of-service, ELD resolution, findings,
        // the prior-records attestation, the audit.
        ComplianceEndpoints.Map(app);

        return app;
    }

    /// <summary>
    /// 403 for a caller who may use this route but does not own this driver's records. Deliberately
    /// 403 and not 404: the row exists, the caller simply is not its owner, and pretending
    /// otherwise would mislead whoever reads the logs. The body matches
    /// <see cref="EndpointResults"/>' <c>{ code, message }</c> shape so clients parse one thing.
    /// </summary>
    internal static IResult NotYourDriverRecord() => Results.Json(
        new { code = "Drivers.NotYourRecord", message = "You may only access your own driver record." },
        statusCode: StatusCodes.Status403Forbidden);

    private static async Task<IResult> GetMyDriverRecord(
        ITenantContext tenantContext, ICurrentActor actor, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        // No sub claim at all is an authentication problem, not a missing link — keep the two
        // apart so the app does not tell a driver to "ask dispatch" about a broken token.
        if (actor.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        // 404 Drivers.NotLinked when this account has no roster row — the Field App renders
        // "your account isn't linked to a driver record, ask dispatch" off that exact code.
        var result = await sender.Query(new GetDriverByUserQuery(tenantId, userId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> LinkDriverUser(
        Guid id, LinkDriverUserRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new LinkDriverUserCommand(tenantId, id, request.UserId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UnlinkDriverUser(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new UnlinkDriverUserCommand(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> SetDriverMotiveUserId(
        Guid id, SetDriverMotiveUserIdRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        // A missing value is sent as "" so the aggregate refuses it (MotiveUserIdRequired)
        // instead of reading it as "clear" — clearing is the DELETE.
        var result = await sender.Send(
            new SetDriverMotiveUserIdCommand(tenantId, id, request.MotiveUserId ?? string.Empty), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ClearDriverMotiveUserId(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new SetDriverMotiveUserIdCommand(tenantId, id, null), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
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
        Guid driverId, ITenantContext tenantContext, IDriverSelfAccess selfAccess, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!await selfAccess.MayActOnDriverAsync(driverId, cancellationToken))
        {
            return NotYourDriverRecord();
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
        Guid driverId, ITenantContext tenantContext, IDriverSelfAccess selfAccess, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!await selfAccess.MayActOnDriverAsync(driverId, cancellationToken))
        {
            return NotYourDriverRecord();
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

/// <summary>Request body for POST /api/drivers/{id}/user — the account's <c>sub</c>.</summary>
public sealed record LinkDriverUserRequest(Guid UserId);

/// <summary>
/// Request body for PUT /api/drivers/{id}/motive — the driver's Motive user id as a string
/// (Motive's JSON carries an integer; send it as text). Trimmed, at most 64 characters, unique per
/// tenant. DELETE on the same route clears it.
/// </summary>
public sealed record SetDriverMotiveUserIdRequest(string? MotiveUserId);
