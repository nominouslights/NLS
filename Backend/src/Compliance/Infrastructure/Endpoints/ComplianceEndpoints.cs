using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.Records;
using NorthernLink.Compliance.Application.Records.Dispose;
using NorthernLink.Compliance.Application.Records.Holds;
using NorthernLink.Compliance.Application.Records.LinkSupportingDocument;
using NorthernLink.Compliance.Application.Records.Print;
using NorthernLink.Compliance.Application.Records.Queries;
using NorthernLink.Compliance.Application.Records.QueueForDisposalReview;
using NorthernLink.Compliance.Application.Records.Register;
using NorthernLink.Compliance.Application.Records.SetAnchorDate;
using NorthernLink.Compliance.Application.Records.Supersede;
using NorthernLink.Compliance.Application.RegulatoryBasis;
using NorthernLink.Compliance.Application.RuleSets;
using NorthernLink.Compliance.Application.Settings;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Compliance.Infrastructure.Retention;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Compliance.Infrastructure.Endpoints;

/// <summary>
/// The Compliance module's minimal-API surface under <c>/api/compliance</c>. The whole group
/// carries the <see cref="AuthorizationPolicies.ComplianceAccess"/> policy (Owner + ComplianceOfficer
/// only). Every endpoint additionally resolves the ambient tenant (401 when absent — the API half
/// of dual tenant enforcement), stamps it onto the command/query, and dispatches via
/// <see cref="ISender"/>. Error bodies carry <c>basis</c> keys (see <see cref="EndpointResults"/>).
/// </summary>
public static class ComplianceEndpoints
{
    public static IEndpointRouteBuilder MapComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        var compliance = app.MapGroup("/api/compliance")
            .RequireAuthorization(AuthorizationPolicies.ComplianceAccess);

        // Rule sets — reference data, published from code. Publish is body-less and idempotent.
        compliance.MapGet("rule-sets", ListRuleSets);
        compliance.MapPost("rule-sets/publish", PublishRuleSets);
        compliance.MapPost("tenants/rule-set/apply", ApplyRuleSet);

        // The rules in force for this tenant. {key} is a dotted rule key (hos.dailyDrivingSeconds).
        compliance.MapGet("rules", GetActiveRules);
        compliance.MapGet("rules/{key}", GetRuleValue);

        // The s. 11 catalogue — what a registration form offers.
        compliance.MapGet("record-types", GetRecordTypes);

        // The record registry. Literal segments (dispose, disposal-review, due-for-disposal,
        // retention-status) sit beside {id:guid} routes: a literal can never bind as a guid, so
        // there is no collision — keep the :guid constraint on every id route.
        compliance.MapGet("records", ListRecords);
        compliance.MapPost("records", RegisterRecord);
        compliance.MapGet("records/due-for-disposal", ListDueForDisposal);
        compliance.MapGet("records/retention-status", GetRetentionStatus);
        compliance.MapPost("records/dispose", DisposeRecords);
        compliance.MapPost("records/disposal-review/sweep", SweepDisposalReview);
        compliance.MapGet("records/{id:guid}", GetRecord);
        compliance.MapGet("records/{id:guid}/print", PrintRecord);
        compliance.MapPost("records/{id:guid}/links", LinkSupportingDocument);
        compliance.MapPost("records/{id:guid}/anchors", SetAnchorDate);
        compliance.MapPost("records/{id:guid}/holds", PlaceHold);
        compliance.MapDelete("records/{id:guid}/holds/{holdId:guid}", ReleaseHold);
        compliance.MapPost("records/{id:guid}/supersede", SupersedeRecord);

        compliance.MapGet("destruction-log", ListDestructionLog);

        // The regulatory-basis catalogue — reference data.
        compliance.MapGet("regulatory-basis", ListRegulatoryBasis);
        compliance.MapGet("regulatory-basis/by-keys", GetRegulatoryBasisByKeys);

        // Tenant settings and the one-shot seed.
        compliance.MapGet("settings", GetSettings);
        compliance.MapPut("settings", UpdateSettings);
        compliance.MapPost("seed-defaults", SeedDefaults);

        // Epic 2 — carrier authority, vehicles, insurance and registration (AuthorityEndpoints.cs).
        compliance.MapAuthorityEndpoints();

        return app;
    }

    // --- Rule sets ---------------------------------------------------------------------------------

    private static async Task<IResult> ListRuleSets(ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new ListRuleSetsQuery(), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>Publishes every in-code rule set not yet in the table: 200 with one row per set, <c>created</c> false on a re-run.</summary>
    private static async Task<IResult> PublishRuleSets(
        ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new PublishRuleSetsCommand(currentActor.UserId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>Adopts a published rule set from now on: 201 with the application id.</summary>
    private static async Task<IResult> ApplyRuleSet(
        ApplyRuleSetRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new ApplyTenantRuleSetCommand(tenantId, request.RuleSetId, request.ChangeNotice, currentActor.UserId), cancellationToken);
        return result.IsSuccess
            ? Results.Created("/api/compliance/rules", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetActiveRules(
        DateOnly? asOf, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetActiveRulesQuery(tenantId, asOf ?? Today()), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetRuleValue(
        string key, DateOnly? asOf, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetRuleValueQuery(tenantId, key, asOf ?? Today()), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    // --- Records -----------------------------------------------------------------------------------

    private static async Task<IResult> GetRecordTypes(ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetRecordTypeCatalogueQuery(), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// Lists records. Filters are optional query strings: <c>recordType</c> (R1..R21), <c>subjectKind</c>,
    /// <c>subjectId</c>, <c>status</c>, <c>dueBefore</c> (date). An unknown enum name is a 400.
    /// </summary>
    private static async Task<IResult> ListRecords(
        string? recordType,
        string? subjectKind,
        Guid? subjectId,
        string? status,
        DateOnly? dueBefore,
        ITenantContext tenantContext,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        ComplianceRecordType? type = null;
        if (recordType is not null)
        {
            if (!ComplianceRecordCatalogue.TryParse(recordType, out var parsedType))
            {
                return EndpointResults.Problem(ComplianceRecordErrors.UnknownRecordType);
            }

            type = parsedType;
        }

        RecordSubjectKind? kind = null;
        if (subjectKind is not null)
        {
            if (!Enum.TryParse<RecordSubjectKind>(subjectKind, ignoreCase: false, out var parsedKind))
            {
                return EndpointResults.Problem(ComplianceRecordErrors.UnknownSubjectKind);
            }

            kind = parsedKind;
        }

        ComplianceRecordStatus? recordStatus = null;
        if (status is not null)
        {
            if (!Enum.TryParse<ComplianceRecordStatus>(status, ignoreCase: false, out var parsedStatus))
            {
                return Results.BadRequest(new { code = "Compliance.Record.UnknownStatus", message = "status must be Active, ReviewForDisposal, Disposed or Superseded.", basis = Array.Empty<string>() });
            }

            recordStatus = parsedStatus;
        }

        var result = await sender.Query(
            new ListRecordsQuery(tenantId, new RecordListFilter(type, kind, subjectId, recordStatus, dueBefore)), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// Registers a record by hand (producer modules register through the integration event):
    /// 201 with <c>{ recordId, documentNumber, created }</c>, or 200 with <c>created = false</c> when
    /// the (sourceModule, sourceRecordId) pair was already registered.
    /// </summary>
    private static async Task<IResult> RegisterRecord(
        RegisterRecordRequest request,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!ComplianceRecordCatalogue.TryParse(request.RecordType, out var recordType))
        {
            return EndpointResults.Problem(ComplianceRecordErrors.UnknownRecordType);
        }

        var registration = request.ToRegistration();
        if (registration.IsFailure)
        {
            return EndpointResults.Problem(registration.Error);
        }

        var result = await sender.Send(
            new RegisterRecordCommand(tenantId, recordType, registration.Value, currentActor.UserId), cancellationToken);
        if (result.IsFailure)
        {
            return EndpointResults.Problem(result.Error);
        }

        return result.Value.Created
            ? Results.Created($"/api/compliance/records/{result.Value.RecordId}", result.Value)
            : Results.Ok(result.Value);
    }

    private static async Task<IResult> GetRecord(Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetRecordQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>The record's cover sheet as <c>application/pdf</c>.</summary>
    private static async Task<IResult> PrintRecord(Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetRecordPrintQuery(tenantId, id), cancellationToken);
        return result.IsSuccess
            ? Results.File(result.Value.Content, "application/pdf", result.Value.FileName)
            : EndpointResults.Problem(result.Error);
    }

    private static Task<IResult> LinkSupportingDocument(
        Guid id, SupportingLinkRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext, sender,
            tenantId => new LinkSupportingDocumentCommand(
                tenantId, id, new SupportingLink(request.Kind ?? string.Empty, request.Reference ?? string.Empty, request.Label ?? string.Empty), currentActor.UserId),
            cancellationToken);

    private static async Task<IResult> SetAnchorDate(
        Guid id, SetAnchorDateRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!Enum.TryParse<AnchorKind>(request.Anchor, ignoreCase: false, out var anchor))
        {
            return EndpointResults.Problem(ComplianceRecordErrors.UnknownAnchorKind);
        }

        if (request.Date is not { } date)
        {
            return EndpointResults.Problem(ComplianceRecordErrors.AnchorDateRequired);
        }

        var result = await sender.Send(new SetAnchorDateCommand(tenantId, id, anchor, date, currentActor.UserId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }

    /// <summary>Places a hold: 201 with <c>{ id }</c> (the hold id) — released with DELETE .../holds/{holdId}.</summary>
    private static async Task<IResult> PlaceHold(
        Guid id, PlaceHoldRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new PlaceHoldCommand(tenantId, id, request.Reason, currentActor.UserId), cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/compliance/records/{id}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static Task<IResult> ReleaseHold(
        Guid id, Guid holdId, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken) =>
        SendCommand(tenantContext, sender, tenantId => new ReleaseHoldCommand(tenantId, id, holdId, currentActor.UserId), cancellationToken);

    /// <summary>Registers a new version: 201 with the successor's <c>{ recordId, documentNumber, created }</c>.</summary>
    private static async Task<IResult> SupersedeRecord(
        Guid id, RegisterRecordRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var registration = request.ToRegistration();
        if (registration.IsFailure)
        {
            return EndpointResults.Problem(registration.Error);
        }

        var result = await sender.Send(new SupersedeRecordCommand(tenantId, id, registration.Value, currentActor.UserId), cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/compliance/records/{result.Value.RecordId}", result.Value)
            : EndpointResults.Problem(result.Error);
    }

    /// <summary>Batch disposal: 200 with <c>{ disposed[], refused[] }</c> — every id is accounted for on one side.</summary>
    private static async Task<IResult> DisposeRecords(
        DisposeRecordsRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
            new DisposeRecordsCommand(tenantId, request.RecordIds ?? [], request.Method, request.Note, currentActor.UserId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// Runs the nightly sweep now for this tenant: 200 with <c>{ queued }</c>. <c>asOf</c> may only
    /// look back — a future date would put not-yet-due records in review, and review is what
    /// disposal trusts (400 <c>Compliance.Record.SweepDateInFuture</c>).
    /// </summary>
    private static async Task<IResult> SweepDisposalReview(
        DateOnly? asOf, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var today = Today();
        if (asOf > today)
        {
            return EndpointResults.Problem(ComplianceRecordErrors.SweepDateInFuture);
        }

        var result = await sender.Send(new QueueForDisposalReviewCommand(tenantId, asOf ?? today), cancellationToken);
        return result.IsSuccess ? Results.Ok(new SweepResponse(result.Value)) : EndpointResults.Problem(result.Error);
    }

    /// <summary><c>dueBefore</c> defaults to today plus <see cref="RetentionOptions.ReviewWindowDays"/>.</summary>
    private static async Task<IResult> ListDueForDisposal(
        DateOnly? dueBefore, ITenantContext tenantContext, RetentionOptions options, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(
            new ListRecordsDueForDisposalQuery(tenantId, dueBefore ?? Today().AddDays(options.ReviewWindowDays)), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetRetentionStatus(
        string? subjectKind, Guid? subjectId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        if (!Enum.TryParse<RecordSubjectKind>(subjectKind, ignoreCase: false, out var kind))
        {
            return EndpointResults.Problem(ComplianceRecordErrors.UnknownSubjectKind);
        }

        var result = await sender.Query(new GetRetentionStatusQuery(tenantId, kind, subjectId, Today()), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> ListDestructionLog(
        DateOnly? from, DateOnly? to, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new ListDestructionLogQuery(tenantId, from, to), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    // --- Regulatory basis --------------------------------------------------------------------------

    private static async Task<IResult> ListRegulatoryBasis(
        string? instrument, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        RegulatoryInstrument? parsed = null;
        if (instrument is not null)
        {
            if (!Enum.TryParse<RegulatoryInstrument>(instrument, ignoreCase: false, out var value))
            {
                return Results.BadRequest(new { code = "Compliance.RegulatoryBasis.UnknownInstrument", message = "instrument must be one of HTA, NSC, ManitobaRegulation, FederalRegulation, SafetyPlan, CompanyPolicy, ManitobaGuidance, FederalGuidance.", basis = Array.Empty<string>() });
            }

            parsed = value;
        }

        var result = await sender.Query(new ListRegulatoryBasisQuery(parsed), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary><c>?keys=HTA.318.6.1&amp;keys=NSC.13.16</c> — repeat the parameter per key.</summary>
    private static async Task<IResult> GetRegulatoryBasisByKeys(
        string[]? keys, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetRegulatoryBasisQuery(keys ?? []), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    // --- Settings and seed -------------------------------------------------------------------------

    private static async Task<IResult> GetSettings(ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetComplianceSettingsQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static Task<IResult> UpdateSettings(
        ComplianceSettingsRequest request, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext, sender,
            tenantId => new UpdateComplianceSettingsCommand(tenantId, request.ToDetails(), currentActor.UserId),
            cancellationToken);

    /// <summary>Idempotent one-shot seed: 200 with what it created this time (zeros on a re-run).</summary>
    private static async Task<IResult> SeedDefaults(
        ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new SeedComplianceDefaultsCommand(tenantId, currentActor.UserId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    // --- Plumbing ----------------------------------------------------------------------------------

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Resolve tenant → build command → dispatch → 204, for every bodiless write.</summary>
    private static async Task<IResult> SendCommand(
        ITenantContext tenantContext,
        ISender sender,
        Func<Guid, ICommand> buildCommand,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(buildCommand(tenantId), cancellationToken);
        return result.IsSuccess ? Results.NoContent() : EndpointResults.Problem(result.Error);
    }
}

/// <summary>Body of a successful create (201, with Location header).</summary>
public sealed record EntityCreatedResponse(Guid Id);

/// <summary>Body of POST records/disposal-review/sweep.</summary>
public sealed record SweepResponse(int Queued);

/// <summary>Request body for POST tenants/rule-set/apply. <c>ruleSetId</c> null → the newest published CA-MB set.</summary>
public sealed record ApplyRuleSetRequest(Guid? RuleSetId, string? ChangeNotice);

/// <summary>
/// Request body for POST records and POST records/{id}/supersede. Every field nullable on the
/// wire so a missing one fails as a readable domain error, never a model-binding 400 without a
/// code. Enums travel as names: <c>recordType</c> R1..R21, <c>subjectKind</c>
/// Carrier|Driver|Vehicle|Trip|Incident, <c>anchorDates</c> keyed by anchor name
/// (RecordDate, ReceivedDate, MonthEnd, RepairDate, CompletionDate, EmploymentEnd,
/// VehicleLeftControl, ClaimClosed, Superseded). On supersede, <c>recordType</c> is ignored — the
/// successor keeps the predecessor's type.
/// </summary>
public sealed record RegisterRecordRequest(
    string? RecordType,
    string? Title,
    string? SubjectKind,
    Guid? SubjectId,
    string? SubjectDisplay,
    string? SourceModule,
    string? SourceRecordId,
    IReadOnlyDictionary<string, DateOnly>? AnchorDates,
    IReadOnlyList<SupportingLinkRequest>? SupportingLinks,
    IReadOnlyList<string>? RegulatoryBasisKeys,
    string? PrintTemplateKey)
{
    public Result<RecordRegistration> ToRegistration()
    {
        RecordSubject? subject = null;
        if (SubjectKind is not null)
        {
            if (!Enum.TryParse<RecordSubjectKind>(SubjectKind, ignoreCase: false, out var kind))
            {
                return Result.Failure<RecordRegistration>(ComplianceRecordErrors.UnknownSubjectKind);
            }

            subject = new RecordSubject(kind, SubjectId, SubjectDisplay ?? string.Empty);
        }

        var anchors = new Dictionary<AnchorKind, DateOnly>();
        foreach (var (name, date) in AnchorDates ?? new Dictionary<string, DateOnly>())
        {
            if (!Enum.TryParse<AnchorKind>(name, ignoreCase: false, out var anchor))
            {
                return Result.Failure<RecordRegistration>(ComplianceRecordErrors.UnknownAnchorKind);
            }

            anchors[anchor] = date;
        }

        return Result.Success(new RecordRegistration
        {
            Title = Title,
            Subject = subject,
            SourceModule = SourceModule,
            SourceRecordId = SourceRecordId,
            AnchorDates = anchors,
            SupportingLinks = (SupportingLinks ?? [])
                .Select(l => new SupportingLink(l.Kind ?? string.Empty, l.Reference ?? string.Empty, l.Label ?? string.Empty))
                .ToList(),
            RegulatoryBasisKeys = RegulatoryBasisKeys,
            PrintTemplateKey = PrintTemplateKey,
        });
    }
}

public sealed record SupportingLinkRequest(string? Kind, string? Reference, string? Label);

/// <summary>Request body for POST records/{id}/anchors. <c>anchor</c> is an AnchorKind name.</summary>
public sealed record SetAnchorDateRequest(string? Anchor, DateOnly? Date);

public sealed record PlaceHoldRequest(string? Reason);

/// <summary>Request body for POST records/dispose. <c>method</c> is required ("shredded", "deleted", …).</summary>
public sealed record DisposeRecordsRequest(IReadOnlyList<Guid>? RecordIds, string? Method, string? Note);

/// <summary>Request body for PUT settings — a full replace; omitted optional fields are cleared.</summary>
public sealed record ComplianceSettingsRequest(
    string? CarrierLegalName,
    string? NscNumber,
    string? DocumentPrefix,
    string? PrincipalPlaceOfBusiness,
    string? FileLocationNote)
{
    public ComplianceSettingsDetails ToDetails() => new()
    {
        CarrierLegalName = CarrierLegalName,
        NscNumber = NscNumber,
        DocumentPrefix = DocumentPrefix,
        PrincipalPlaceOfBusiness = PrincipalPlaceOfBusiness,
        FileLocationNote = FileLocationNote,
    };
}
