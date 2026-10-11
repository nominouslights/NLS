using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Drivers.Application.Drivers.AttestPriorRecords;
using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Application.Hos.Audit;
using NorthernLink.Drivers.Application.Hos.EldMalfunctions.Resolve;
using NorthernLink.Drivers.Application.Hos.Exceptions.ApproveDeferral;
using NorthernLink.Drivers.Application.Hos.Exceptions.Reject;
using NorthernLink.Drivers.Application.Hos.Findings.Get;
using NorthernLink.Drivers.Application.Hos.Findings.Open;
using NorthernLink.Drivers.Application.Hos.Findings.Update;
using NorthernLink.Drivers.Application.Hos.OutOfService.Get;
using NorthernLink.Drivers.Application.Hos.OutOfService.Lift;
using NorthernLink.Drivers.Application.Hos.OutOfService.Record;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Infrastructure.Endpoints;

/// <summary>
/// The compliance-officer surface under <c>/api/drivers</c>, mapped by
/// <see cref="DriversEndpoints.MapDriversEndpoints"/>: its own sibling group carrying
/// <see cref="AuthorizationPolicies.ComplianceAccess"/> (Owner and ComplianceOfficer only).
/// These are the decisions that override or stand behind the hours gate — approving a deferral,
/// recording an out-of-service declaration, resolving an ELD malfunction, working findings,
/// attesting prior records, and the audit — so nothing here admits dispatch or a driver. No
/// own-record check: a compliance officer acts on any driver's record, which is the job. The
/// handlers prove the role again underneath the policy.
/// </summary>
public static class ComplianceEndpoints
{
    internal static void Map(IEndpointRouteBuilder app)
    {
        var compliance = app.MapGroup("/api/drivers")
            .RequireAuthorization(AuthorizationPolicies.ComplianceAccess);

        compliance.MapPost("{driverId:guid}/hos/exceptions/deferral", ApproveDeferral);
        compliance.MapPost("{driverId:guid}/hos/exceptions/{id:guid}/reject", RejectException);
        compliance.MapPost("{driverId:guid}/hos/eld-malfunctions/{id:guid}/resolve", ResolveEldMalfunction);
        compliance.MapGet("{driverId:guid}/out-of-service", GetOutOfService);
        compliance.MapPost("{driverId:guid}/out-of-service", RecordOutOfService);
        compliance.MapPost("{driverId:guid}/out-of-service/{id:guid}/lift", LiftOutOfService);
        compliance.MapGet("{driverId:guid}/findings", GetFindings);
        compliance.MapPost("{driverId:guid}/findings", OpenFinding);
        compliance.MapPut("{driverId:guid}/findings/{id:guid}", UpdateFinding);
        compliance.MapPost("{driverId:guid}/prior-records-attestation", AttestPriorRecords);
        compliance.MapGet("{driverId:guid}/hos/audit", GetAudit);
    }

    private static async Task<IResult> ApproveDeferral(
        Guid driverId, ApproveDeferralRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (request.EffectiveFromUtc is not { } effectiveFrom)
        {
            return EndpointResults.Problem(Error.Validation("Drivers.Hos.EffectiveFromRequired", "effectiveFromUtc is required."));
        }

        var command = new ApproveOffDutyDeferralCommand(
            tenantId,
            driverId,
            request.TripId,
            effectiveFrom,
            request.DeferredOffDutyMinutes is { } minutes ? TimeSpan.FromMinutes(minutes) : null,
            request.Reason,
            request.Location,
            request.EldRemarkRecorded ?? false);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/drivers/{driverId}/hos/exceptions/{result.Value.Id}", result.Value)
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RejectException(
        Guid driverId, Guid id, ExceptionNoteRequest? request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new RejectHosExceptionCommand(tenantId, driverId, id, request?.Note), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ResolveEldMalfunction(
        Guid driverId, Guid id, ExceptionNoteRequest? request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new ResolveEldMalfunctionCommand(tenantId, driverId, id, request?.Note), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetOutOfService(
        Guid driverId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetOutOfServiceRecordsQuery(tenantId, driverId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> RecordOutOfService(
        Guid driverId, RecordOutOfServiceRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (request.DeclaredAtUtc is not { } declaredAt)
        {
            return EndpointResults.Problem(Error.Validation("Drivers.Hos.DeclaredAtRequired", "declaredAtUtc is required."));
        }

        var command = new RecordOutOfServiceCommand(
            tenantId, driverId, declaredAt, request.UntilUtc, request.OpenEnded ?? false, request.Reason, request.Evidence);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/drivers/{driverId}/out-of-service/{result.Value.Id}", result.Value)
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> LiftOutOfService(
        Guid driverId, Guid id, LiftOutOfServiceRequest? request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new LiftOutOfServiceCommand(tenantId, driverId, id, request?.Reason), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetFindings(
        Guid driverId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetComplianceFindingsQuery(tenantId, driverId, from, to), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> OpenFinding(
        Guid driverId, OpenFindingRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (request.LocalDate is not { } localDate)
        {
            return EndpointResults.Problem(Error.Validation("Drivers.Hos.LocalDateRequired", "localDate is required."));
        }

        if (!TryParseMember<FindingSeverity>(request.Severity, out var severity))
        {
            return EndpointResults.Problem(ComplianceErrors.FindingSeverityInvalid);
        }

        var source = FindingSource.Audit;
        if (request.Source is not null && !TryParseMember(request.Source, out source))
        {
            return EndpointResults.Problem(ComplianceErrors.FindingSourceInvalid);
        }

        var command = new OpenComplianceFindingCommand(
            tenantId, driverId, localDate, request.Rule, severity, request.Description, source, request.FollowUpNote);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/drivers/{driverId}/findings/{result.Value.Id}", result.Value)
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> UpdateFinding(
        Guid driverId, Guid id, UpdateFindingRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!TryParseMember<FindingStatus>(request.Status, out var status))
        {
            return EndpointResults.Problem(ComplianceErrors.FindingStatusInvalid);
        }

        var result = await sender.Send(
            new UpdateComplianceFindingCommand(tenantId, driverId, id, status, request.FollowUpNote), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> AttestPriorRecords(
        Guid driverId, AttestPriorRecordsRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (request.CoveredThroughDate is not { } coveredThrough)
        {
            return EndpointResults.Problem(Error.Validation("Drivers.Hos.CoveredThroughRequired", "coveredThroughDate is required."));
        }

        var result = await sender.Send(
            new AttestPriorRecordsCommand(tenantId, driverId, coveredThrough, request.Note, request.WorksForOtherCarriers ?? false),
            cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetAudit(
        Guid driverId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        ITenantContext tenantContext,
        ISender sender,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        // Default: the regulation's 14-day window ending today (America/Winnipeg).
        var today = HosLocalTime.DateOf(clock.GetUtcNow());
        var rangeTo = to ?? today;
        var rangeFrom = from ?? rangeTo.AddDays(-13);

        var result = await sender.Query(new GetHosAuditQuery(tenantId, driverId, rangeFrom, rangeTo), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>Enum member names, matched exactly — "Severe" parses, "severe" does not.</summary>
    private static bool TryParseMember<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum =>
        Enum.TryParse(value?.Trim(), ignoreCase: false, out parsed) && Enum.IsDefined(parsed);
}

/// <summary>
/// Request body for <c>POST /api/drivers/{driverId}/hos/exceptions/deferral</c>. <see cref="TripId"/>
/// is required; <see cref="EffectiveFromUtc"/> must be in the future and within seven days;
/// <see cref="DeferredOffDutyMinutes"/> is 1–120.
/// </summary>
public sealed record ApproveDeferralRequest(
    Guid? TripId,
    DateTimeOffset? EffectiveFromUtc,
    int? DeferredOffDutyMinutes,
    string? Reason,
    string? Location = null,
    bool? EldRemarkRecorded = null);

/// <summary>
/// Request body for <c>POST /api/drivers/{driverId}/out-of-service</c>. <see cref="UntilUtc"/>
/// omitted means 72 hours after <see cref="DeclaredAtUtc"/>; <see cref="OpenEnded"/> true means
/// until lifted.
/// </summary>
public sealed record RecordOutOfServiceRequest(
    DateTimeOffset? DeclaredAtUtc,
    string? Reason,
    DateTimeOffset? UntilUtc = null,
    bool? OpenEnded = null,
    string? Evidence = null);

/// <summary>Request body for <c>POST …/out-of-service/{id}/lift</c>. <see cref="Reason"/> is required for an early lift.</summary>
public sealed record LiftOutOfServiceRequest(string? Reason = null);

/// <summary>
/// Request body for <c>POST /api/drivers/{driverId}/findings</c>. <see cref="Rule"/> is an
/// <c>HosRule</c> name or Falsification / Tampering / TwoRecords / OutOfServiceDeclared;
/// <see cref="Severity"/> is Minor / Major / Severe; <see cref="Source"/> is Audit (default) /
/// Enforcement / Auto.
/// </summary>
public sealed record OpenFindingRequest(
    DateOnly? LocalDate,
    string? Rule,
    string? Severity,
    string? Description,
    string? Source = null,
    string? FollowUpNote = null);

/// <summary>Request body for <c>PUT /api/drivers/{driverId}/findings/{id}</c>. <see cref="Status"/> is Open / Corrected / Closed.</summary>
public sealed record UpdateFindingRequest(string? Status, string? FollowUpNote = null);

/// <summary>Request body for <c>POST /api/drivers/{driverId}/prior-records-attestation</c>.</summary>
public sealed record AttestPriorRecordsRequest(
    DateOnly? CoveredThroughDate,
    string? Note = null,
    bool? WorksForOtherCarriers = null);
