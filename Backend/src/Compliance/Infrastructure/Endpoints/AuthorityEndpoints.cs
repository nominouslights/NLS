using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Compliance.Application.Authority;
using NorthernLink.Compliance.Application.Insurance;
using NorthernLink.Compliance.Application.Registrations;
using NorthernLink.Compliance.Application.Vehicles;
using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Insurance;
using NorthernLink.Compliance.Domain.Registrations;
using NorthernLink.Compliance.Domain.Signatures;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Storage;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Compliance.Infrastructure.Endpoints;

/// <summary>
/// Epic 2's surface on the <c>/api/compliance</c> group (so every route inherits
/// <c>ComplianceAccess</c>): the carrier authority, the regulated-vehicle override, insurance
/// policies and vehicle registrations with their attachments. Uploads are multipart
/// (<c>file</c> field) with antiforgery disabled — the API is reached only through the frontends'
/// server-side proxy with a bearer token, the Drivers credential-image precedent. Object storage is
/// taken <c>[FromServices]</c> explicitly so route building never has to guess what it is.
/// </summary>
internal static class AuthorityEndpoints
{
    public const string SfcOwnerKind = "sfc";
    public const string InsuranceOwnerKind = "insurance-policies";
    public const string RegistrationOwnerKind = "vehicle-registrations";

    public static void MapAuthorityEndpoints(this RouteGroupBuilder compliance)
    {
        // The carrier's operating authority — one per tenant, opened on first write.
        compliance.MapGet("authority", GetAuthority);
        compliance.MapPost("authority/sfc", RecordSfc);
        compliance.MapPost("authority/sfc/{certificateId:guid}/certificate", UploadSfcCertificate).DisableAntiforgery();
        compliance.MapGet("authority/sfc/{certificateId:guid}/certificate", DownloadSfcCertificate);
        compliance.MapPost("authority/nsc-number", RecordNscNumber);
        compliance.MapPut("authority/safety-rating", SetSafetyRating);
        compliance.MapPut("authority/intra-provincial", DeclareIntraProvincial);
        compliance.MapPut("authority/routes", SetOperatingRoutes);

        // Vehicles as Compliance sees them (Fleet replica + the regulated override).
        compliance.MapGet("vehicles", ListVehicles);
        compliance.MapPut("vehicles/{vehicleId:guid}/regulated", SetVehicleRegulated);

        // Insurance (R20). Literal sub-paths sit under {id:guid}; keep the constraint on every id route.
        compliance.MapGet("insurance-policies", ListInsurancePolicies);
        compliance.MapPost("insurance-policies", RecordInsurancePolicy);
        compliance.MapGet("insurance-policies/{id:guid}", GetInsurancePolicy);
        compliance.MapPost("insurance-policies/{id:guid}/renew", RenewInsurancePolicy);
        compliance.MapPost("insurance-policies/{id:guid}/certificate", UploadInsuranceCertificate).DisableAntiforgery();
        compliance.MapPost("insurance-policies/{id:guid}/filing-confirmation", UploadInsuranceFilingConfirmation).DisableAntiforgery();
        compliance.MapGet("insurance-policies/{id:guid}/attachments/{slot}", DownloadInsuranceAttachment);

        // Registration (R20).
        compliance.MapGet("vehicle-registrations", ListVehicleRegistrations);
        compliance.MapPost("vehicle-registrations", RecordVehicleRegistration);
        compliance.MapGet("vehicle-registrations/{id:guid}", GetVehicleRegistration);
        compliance.MapPost("vehicle-registrations/{id:guid}/renew", RenewVehicleRegistration);
        compliance.MapPost("vehicle-registrations/{id:guid}/document", UploadRegistrationDocument).DisableAntiforgery();
        compliance.MapGet("vehicle-registrations/{id:guid}/attachments/{slot}", DownloadRegistrationAttachment);
    }

    // --- Authority ---------------------------------------------------------------------------------

    private static async Task<IResult> GetAuthority(ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetCarrierAuthorityQuery(tenantId, Today()), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static Task<IResult> RecordSfc(
        RecordSfcRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken) =>
        Send(tenantContext, sender, tenantId => new RecordSfcCommand(tenantId, request.Number, request.IssuedOn, request.ExpiresOn, currentActor.UserId), cancellationToken);

    /// <summary>Multipart <c>file</c>: the scan of the SFC in force. 409 <c>SfcNotCurrent</c> for any other certificate id.</summary>
    private static Task<IResult> UploadSfcCertificate(
        Guid certificateId, IFormFile? file, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, [FromServices] IObjectStorage storage, CancellationToken cancellationToken) =>
        Upload(
            tenantContext, currentActor, sender, storage, file, SfcOwnerKind, certificateId, AttachmentSlots.Certificate,
            async tenantId =>
            {
                var authority = await sender.Query(new GetCarrierAuthorityQuery(tenantId, Today()), cancellationToken);
                return authority.IsSuccess && authority.Value.CurrentSfc?.CertificateId == certificateId
                    ? null
                    : CarrierAuthorityErrors.SfcNotCurrent;
            },
            (tenantId, attachment) => new AttachSfcCertificateCommand(tenantId, certificateId, attachment, currentActor.UserId),
            cancellationToken);

    private static async Task<IResult> DownloadSfcCertificate(
        Guid certificateId, ITenantContext tenantContext, ISender sender, [FromServices] IObjectStorage storage, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetSfcAttachmentQuery(tenantId, certificateId), cancellationToken);
        return result.IsSuccess ? await StreamAttachment(storage, result.Value, cancellationToken) : EndpointResults.Problem(result.Error);
    }

    private static Task<IResult> RecordNscNumber(
        RecordNscNumberRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken) =>
        Send(tenantContext, sender, tenantId => new RecordNscNumberCommand(tenantId, request.NscNumber, currentActor.UserId), cancellationToken);

    private static Task<IResult> SetSafetyRating(
        SetSafetyRatingRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SafetyRating>(request.Rating, ignoreCase: false, out var rating) || !Enum.IsDefined(rating))
        {
            return Task.FromResult(EndpointResults.Problem(CarrierAuthorityErrors.SafetyRatingUnknown));
        }

        return Send(tenantContext, sender, tenantId => new SetSafetyRatingCommand(tenantId, rating, request.AsOf, currentActor.UserId), cancellationToken);
    }

    private static Task<IResult> DeclareIntraProvincial(
        DeclareIntraProvincialRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SignatoryRole>(request.SignerRole, ignoreCase: false, out var role) || !Enum.IsDefined(role))
        {
            return Task.FromResult(EndpointResults.Problem(SignatureErrors.RoleUnknown));
        }

        var signature = new SignatureInput(role, request.SignerName, request.Phone, request.Email, request.Statement, ImageKey: null);
        return Send(tenantContext, sender, tenantId => new DeclareIntraProvincialCommand(tenantId, request.DeclaredOn, signature, currentActor.UserId), cancellationToken);
    }

    private static Task<IResult> SetOperatingRoutes(
        SetOperatingRoutesRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken) =>
        Send(tenantContext, sender, tenantId => new SetOperatingRoutesCommand(tenantId, request.Markdown, currentActor.UserId), cancellationToken);

    // --- Vehicles ----------------------------------------------------------------------------------

    private static async Task<IResult> ListVehicles(ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new ListComplianceVehiclesQuery(tenantId, Today()), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary><c>{ "regulatedOverride": true | false | null }</c> — null clears the override (seating decides again).</summary>
    private static Task<IResult> SetVehicleRegulated(
        Guid vehicleId, SetVehicleRegulatedRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken) =>
        Send(tenantContext, sender, tenantId => new SetVehicleRegulatedCommand(tenantId, vehicleId, request.RegulatedOverride, currentActor.UserId), cancellationToken);

    // --- Insurance ---------------------------------------------------------------------------------

    private static async Task<IResult> ListInsurancePolicies(
        Guid? vehicleId, bool? includeSuperseded, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new ListInsurancePoliciesQuery(tenantId, vehicleId, includeSuperseded ?? false), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>201 with <c>{ id }</c>. A limit below the minimum is saved; the response's <c>belowMinimum</c> says so.</summary>
    private static async Task<IResult> RecordInsurancePolicy(
        InsurancePolicyRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (request.VehicleId is not { } vehicleId)
        {
            return EndpointResults.Problem(ComplianceVehicleErrors.VehicleIdRequired);
        }

        var result = await sender.Send(new RecordInsurancePolicyCommand(tenantId, vehicleId, request.ToDetails(), currentActor.UserId), cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/compliance/insurance-policies/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetInsurancePolicy(Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetInsurancePolicyQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>201 with the successor's <c>{ id }</c>; the renewed policy becomes Superseded. <c>vehicleId</c> in the body is ignored.</summary>
    private static async Task<IResult> RenewInsurancePolicy(
        Guid id, InsurancePolicyRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new RenewInsurancePolicyCommand(tenantId, id, request.ToDetails(), currentActor.UserId), cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/compliance/insurance-policies/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static Task<IResult> UploadInsuranceCertificate(
        Guid id, IFormFile? file, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, [FromServices] IObjectStorage storage, CancellationToken cancellationToken) =>
        UploadInsurance(id, AttachmentSlots.Certificate, file, tenantContext, currentActor, sender, storage, cancellationToken);

    private static Task<IResult> UploadInsuranceFilingConfirmation(
        Guid id, IFormFile? file, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, [FromServices] IObjectStorage storage, CancellationToken cancellationToken) =>
        UploadInsurance(id, AttachmentSlots.FilingConfirmation, file, tenantContext, currentActor, sender, storage, cancellationToken);

    private static Task<IResult> UploadInsurance(
        Guid id, string slot, IFormFile? file, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, IObjectStorage storage, CancellationToken cancellationToken) =>
        Upload(
            tenantContext, currentActor, sender, storage, file, InsuranceOwnerKind, id, slot,
            async tenantId => (await sender.Query(new GetInsurancePolicyQuery(tenantId, id), cancellationToken)).IsFailure
                ? InsuranceErrors.NotFound
                : null,
            (tenantId, attachment) => new AttachInsuranceDocumentCommand(tenantId, id, slot, attachment, currentActor.UserId),
            cancellationToken);

    private static async Task<IResult> DownloadInsuranceAttachment(
        Guid id, string slot, ITenantContext tenantContext, ISender sender, [FromServices] IObjectStorage storage, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetInsuranceAttachmentQuery(tenantId, id, slot), cancellationToken);
        return result.IsSuccess ? await StreamAttachment(storage, result.Value, cancellationToken) : EndpointResults.Problem(result.Error);
    }

    // --- Registration ------------------------------------------------------------------------------

    private static async Task<IResult> ListVehicleRegistrations(
        Guid? vehicleId, bool? includeSuperseded, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new ListVehicleRegistrationsQuery(tenantId, vehicleId, includeSuperseded ?? false), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>201 with <c>{ id }</c>. Without <c>expiresOn</c> the term is the rule set's default (6 months) from <c>effectiveOn</c>.</summary>
    private static async Task<IResult> RecordVehicleRegistration(
        VehicleRegistrationRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (request.VehicleId is not { } vehicleId)
        {
            return EndpointResults.Problem(ComplianceVehicleErrors.VehicleIdRequired);
        }

        var result = await sender.Send(new RecordVehicleRegistrationCommand(tenantId, vehicleId, request.ToDetails(), currentActor.UserId), cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/compliance/vehicle-registrations/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetVehicleRegistration(Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetVehicleRegistrationQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>201 with the successor's <c>{ id }</c>. Plate and class carry over unless the body names new ones.</summary>
    private static async Task<IResult> RenewVehicleRegistration(
        Guid id, VehicleRegistrationRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new RenewVehicleRegistrationCommand(tenantId, id, request.ToDetails(), currentActor.UserId), cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/compliance/vehicle-registrations/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static Task<IResult> UploadRegistrationDocument(
        Guid id, IFormFile? file, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, [FromServices] IObjectStorage storage, CancellationToken cancellationToken) =>
        Upload(
            tenantContext, currentActor, sender, storage, file, RegistrationOwnerKind, id, AttachmentSlots.Document,
            async tenantId => (await sender.Query(new GetVehicleRegistrationQuery(tenantId, id), cancellationToken)).IsFailure
                ? RegistrationErrors.NotFound
                : null,
            (tenantId, attachment) => new AttachRegistrationDocumentCommand(tenantId, id, AttachmentSlots.Document, attachment, currentActor.UserId),
            cancellationToken);

    private static async Task<IResult> DownloadRegistrationAttachment(
        Guid id, string slot, ITenantContext tenantContext, ISender sender, [FromServices] IObjectStorage storage, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetRegistrationAttachmentQuery(tenantId, id, slot), cancellationToken);
        return result.IsSuccess ? await StreamAttachment(storage, result.Value, cancellationToken) : EndpointResults.Problem(result.Error);
    }

    // --- Plumbing ----------------------------------------------------------------------------------

    /// <summary>
    /// The shared upload path: validate the file (type, size) before a byte is stored, confirm the
    /// owner exists (no orphan objects for a mistyped id), store under a fresh key, then attach. If
    /// the attach is refused the stored object is deleted again, best effort. A replaced file's
    /// object is deliberately kept — R20 keeps superseded copies.
    /// 200 with the attachment's <c>{ fileName, contentType, sizeBytes, uploadedAtUtc }</c>.
    /// </summary>
    private static async Task<IResult> Upload(
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        IObjectStorage storage,
        IFormFile? file,
        string ownerKind,
        Guid ownerId,
        string slot,
        Func<Guid, Task<Error?>> ownerMissing,
        Func<Guid, AttachmentRef, ICommand> attach,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (file is null)
        {
            return EndpointResults.Problem(AttachmentErrors.FileRequired);
        }

        var valid = AttachmentRef.Validate(file.ContentType, file.Length);
        if (valid.IsFailure)
        {
            return EndpointResults.Problem(valid.Error);
        }

        if (await ownerMissing(tenantId) is { } missing)
        {
            return EndpointResults.Problem(missing);
        }

        var key = AttachmentRef.BuildKey(tenantId, ownerKind, ownerId, slot, file.FileName, file.ContentType);
        var attachment = AttachmentRef.Create(key, file.FileName, file.ContentType, file.Length, DateTimeOffset.UtcNow, currentActor.UserId);
        if (attachment.IsFailure)
        {
            return EndpointResults.Problem(attachment.Error);
        }

        try
        {
            await using var stream = file.OpenReadStream();
            await storage.PutAsync(key, stream, attachment.Value.ContentType, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return EndpointResults.Problem(AttachmentErrors.StorageUnavailable);
        }

        var result = await sender.Send(attach(tenantId, attachment.Value), cancellationToken);
        if (result.IsFailure)
        {
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Best effort: an orphaned object under compliance/{tenant}/… is harmless and traceable by key.
            }

            return EndpointResults.Problem(result.Error);
        }

        return Results.Ok(AttachmentResponse.From(attachment.Value));
    }

    private static async Task<IResult> StreamAttachment(IObjectStorage storage, AttachmentRef attachment, CancellationToken cancellationToken)
    {
        try
        {
            var stream = await storage.GetAsync(attachment.StorageKey, cancellationToken);
            return Results.File(stream, attachment.ContentType, attachment.FileName, enableRangeProcessing: true);
        }
        catch (KeyNotFoundException)
        {
            return EndpointResults.Problem(AttachmentErrors.NotAttached);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return EndpointResults.Problem(AttachmentErrors.StorageUnavailable);
        }
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<IResult> Send(ITenantContext tenantContext, ISender sender, Func<Guid, ICommand> buildCommand, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(buildCommand(tenantId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }
}

/// <summary>Body of POST authority/sfc.</summary>
public sealed record RecordSfcRequest(string? Number, DateOnly? IssuedOn, DateOnly? ExpiresOn);

/// <summary>Body of POST authority/nsc-number.</summary>
public sealed record RecordNscNumberRequest(string? NscNumber);

/// <summary>Body of PUT authority/safety-rating. <c>rating</c>: Unaudited | Satisfactory | Conditional | Unsatisfactory.</summary>
public sealed record SetSafetyRatingRequest(string? Rating, DateOnly? AsOf);

/// <summary>
/// Body of PUT authority/intra-provincial. <c>signerRole</c> must be <c>Owner</c>; <c>statement</c>
/// is the text the owner saw and signed, kept verbatim.
/// </summary>
public sealed record DeclareIntraProvincialRequest(
    DateOnly? DeclaredOn, string? SignerRole, string? SignerName, string? Phone, string? Email, string? Statement);

/// <summary>Body of PUT authority/routes — markdown; blank clears.</summary>
public sealed record SetOperatingRoutesRequest(string? Markdown);

/// <summary>Body of PUT vehicles/{vehicleId}/regulated.</summary>
public sealed record SetVehicleRegulatedRequest(bool? RegulatedOverride);

/// <summary>Body of POST insurance-policies (and of …/renew, where <c>vehicleId</c> is ignored). <c>insurer</c> defaults to MPI.</summary>
public sealed record InsurancePolicyRequest(
    Guid? VehicleId,
    string? Insurer,
    string? PolicyNumber,
    decimal? LiabilityLimitCad,
    decimal? CargoCoverageCad,
    DateOnly? EffectiveOn,
    DateOnly? ExpiresOn)
{
    public InsurancePolicyDetails ToDetails() => new()
    {
        Insurer = Insurer,
        PolicyNumber = PolicyNumber,
        LiabilityLimitCad = LiabilityLimitCad,
        CargoCoverageCad = CargoCoverageCad,
        EffectiveOn = EffectiveOn,
        ExpiresOn = ExpiresOn,
    };
}

/// <summary>Body of POST vehicle-registrations (and of …/renew, where <c>vehicleId</c> is ignored). Class defaults to AR.</summary>
public sealed record VehicleRegistrationRequest(
    Guid? VehicleId,
    string? Plate,
    string? VehicleClass,
    int? TermMonths,
    DateOnly? EffectiveOn,
    DateOnly? ExpiresOn)
{
    public VehicleRegistrationDetails ToDetails() => new()
    {
        Plate = Plate,
        VehicleClass = VehicleClass,
        TermMonths = TermMonths,
        EffectiveOn = EffectiveOn,
        ExpiresOn = ExpiresOn,
    };
}
