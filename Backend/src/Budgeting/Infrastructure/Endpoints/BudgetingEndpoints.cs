using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Budgeting.Application.Codes.Create;
using NorthernLink.Budgeting.Application.Codes.Delete;
using NorthernLink.Budgeting.Application.Codes.GetCodes;
using NorthernLink.Budgeting.Application.Codes.GetOwnerCandidates;
using NorthernLink.Budgeting.Application.Codes.SeedStarterSet;
using NorthernLink.Budgeting.Application.Codes.SetActive;
using NorthernLink.Budgeting.Application.Codes.Update;
using NorthernLink.Budgeting.Application.Periods.Create;
using NorthernLink.Budgeting.Application.Periods.GetPeriods;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Infrastructure.Endpoints;

/// <summary>
/// The Budgeting module's minimal-API surface under <c>/api/budgeting</c>. The whole group
/// carries the <see cref="AuthorizationPolicies.BudgetAccess"/> policy (Owner + Accountant
/// only — the security boundary behind the Budgeting console's client-side UX gate). Every
/// endpoint additionally resolves the ambient tenant (401 when absent — the API half of
/// dual tenant enforcement), stamps it onto the command/query, and dispatches via
/// <see cref="ISender"/>.
/// </summary>
public static class BudgetingEndpoints
{
    public static IEndpointRouteBuilder MapBudgetingEndpoints(this IEndpointRouteBuilder app)
    {
        var budgeting = app.MapGroup("/api/budgeting")
            .RequireAuthorization(AuthorizationPolicies.BudgetAccess);

        // Periods.
        budgeting.MapGet("periods", GetPeriods);
        budgeting.MapPost("periods", CreatePeriod);

        // Codes. Retiring (activate/deactivate) is the normal end-of-life path and stays that
        // way: allocations and actuals reference codes by string and must keep resolving, so a
        // code that has ever been used is retired, never deleted. DELETE exists only for the
        // narrow case retirement does not cover — a code created in error and never referenced —
        // and IBudgetCodeUsageProbe turns it into a 409 the moment anything points at the code.
        budgeting.MapGet("codes", GetCodes);
        budgeting.MapGet("codes/owners", GetOwnerCandidates);
        budgeting.MapPost("codes", CreateCode);
        budgeting.MapPut("codes/{id:guid}", UpdateCode);
        budgeting.MapPost("codes/{id:guid}/activate", ActivateCode);
        budgeting.MapPost("codes/{id:guid}/deactivate", DeactivateCode);
        budgeting.MapDelete("codes/{id:guid}", DeleteCode);
        budgeting.MapPost("codes/starter-set", SeedStarterSet);

        return app;
    }

    private static async Task<IResult> GetPeriods(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetBudgetPeriodsQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreatePeriod(
        CreateBudgetPeriodRequest request, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateBudgetPeriodCommand(
            tenantId, request.Granularity, request.Year, request.Ordinal);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/budgeting/periods/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetCodes(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetBudgetCodesQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetOwnerCandidates(
        ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetBudgetOwnerCandidatesQuery(tenantId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> CreateCode(
        CreateBudgetCodeRequest request,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateBudgetCodeCommand(
            tenantId, request.Code ?? string.Empty, request.ToDetails(), currentActor.UserId);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/budgeting/codes/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static Task<IResult> UpdateCode(
        Guid id,
        UpdateBudgetCodeRequest request,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken) =>
        SendCodeCommand(
            tenantContext,
            sender,
            tenantId => new UpdateBudgetCodeCommand(tenantId, id, request.ToDetails(), currentActor.UserId),
            cancellationToken);

    private static Task<IResult> ActivateCode(
        Guid id,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken) =>
        SendCodeCommand(
            tenantContext,
            sender,
            tenantId => new SetBudgetCodeActiveCommand(tenantId, id, true, currentActor.UserId),
            cancellationToken);

    private static Task<IResult> DeactivateCode(
        Guid id,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken) =>
        SendCodeCommand(
            tenantContext,
            sender,
            tenantId => new SetBudgetCodeActiveCommand(tenantId, id, false, currentActor.UserId),
            cancellationToken);

    // No actor: a deleted row has nowhere to record who deleted it. See DeleteBudgetCodeCommand.
    private static Task<IResult> DeleteCode(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken) =>
        SendCodeCommand(
            tenantContext, sender, tenantId => new DeleteBudgetCodeCommand(tenantId, id), cancellationToken);

    /// <summary>
    /// Creates any of the starter chart the tenant does not already have. 200 with a count rather
    /// than 201: it is idempotent, creates many rows or none, and there is no single new resource
    /// to point a Location header at.
    /// </summary>
    private static async Task<IResult> SeedStarterSet(
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
            new SeedStarterBudgetCodesCommand(tenantId, currentActor.UserId), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(new StarterSetSeededResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// Resolve tenant → build command → dispatch → 204. The command is built by a callback
    /// rather than passed in, so it can carry the tenant id the guard just proved exists.
    /// </summary>
    private static async Task<IResult> SendCodeCommand(
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

/// <summary>
/// Body of POST /api/budgeting/codes/starter-set. <paramref name="Created"/> counts only the
/// codes actually added — zero on a re-run, which is a success, not a failure.
/// </summary>
public sealed record StarterSetSeededResponse(int Created);

/// <summary>
/// Request body for POST /api/budgeting/periods. Granularity is the enum name ("Month" or
/// "Quarter"); Ordinal is 1-12 / 1-4. Dates and label are derived server-side — never sent.
/// A missing Year/Ordinal binds to 0 and fails domain validation with a 400.
/// </summary>
public sealed record CreateBudgetPeriodRequest(
    PeriodGranularity Granularity,
    int Year,
    int Ordinal);

/// <summary>
/// Request body for POST /api/budgeting/codes. Every string is nullable on the wire so a missing
/// field fails as a readable domain validation error rather than a model-binding 400 with no code
/// in it. Enums travel as their names ("Revenue", "Nihb", "GstApplicable", "Quarterly"). The code
/// string is normalized server-side (trim + upper case) and cannot be changed afterwards.
/// <para>
/// <see cref="ReviewFrequency"/> is nullable here even though the field is required, and that is
/// deliberate: a non-nullable enum binds an omitted JSON property to value 0 — <c>Monthly</c> —
/// so a client that forgot the field would silently store the wrong cadence instead of getting
/// the documented default. Nullable + <c>?? Quarterly</c> makes "required, default Quarterly"
/// true on the wire as well as in the model.
/// </para>
/// </summary>
public sealed record CreateBudgetCodeRequest(
    string? Code,
    string? Name,
    string? Description,
    BudgetCodeCategory Category,
    BudgetServiceLine? ServiceLine,
    string? CostCentre,
    Guid? ParentCodeId,
    string? GlAccountCode,
    BudgetTaxTreatment? TaxTreatment,
    Guid? BudgetOwnerUserId,
    BudgetReviewFrequency? ReviewFrequency)
{
    public BudgetCodeDetails ToDetails() => new()
    {
        Name = Name ?? string.Empty,
        Description = Description,
        Category = Category,
        ServiceLine = ServiceLine,
        CostCentre = CostCentre,
        ParentCodeId = ParentCodeId,
        GlAccountCode = GlAccountCode,
        TaxTreatment = TaxTreatment,
        BudgetOwnerUserId = BudgetOwnerUserId,
        ReviewFrequency = ReviewFrequency ?? BudgetReviewFrequency.Quarterly,
    };
}

/// <summary>
/// Request body for PUT /api/budgeting/codes/{id}. Carries no Code: the code string is set once
/// at creation and is not renameable — allocations and actuals reference it by string. Same
/// nullable-enum reasoning as <see cref="CreateBudgetCodeRequest"/>.
/// </summary>
public sealed record UpdateBudgetCodeRequest(
    string? Name,
    string? Description,
    BudgetCodeCategory Category,
    BudgetServiceLine? ServiceLine,
    string? CostCentre,
    Guid? ParentCodeId,
    string? GlAccountCode,
    BudgetTaxTreatment? TaxTreatment,
    Guid? BudgetOwnerUserId,
    BudgetReviewFrequency? ReviewFrequency)
{
    public BudgetCodeDetails ToDetails() => new()
    {
        Name = Name ?? string.Empty,
        Description = Description,
        Category = Category,
        ServiceLine = ServiceLine,
        CostCentre = CostCentre,
        ParentCodeId = ParentCodeId,
        GlAccountCode = GlAccountCode,
        TaxTreatment = TaxTreatment,
        BudgetOwnerUserId = BudgetOwnerUserId,
        ReviewFrequency = ReviewFrequency ?? BudgetReviewFrequency.Quarterly,
    };
}
