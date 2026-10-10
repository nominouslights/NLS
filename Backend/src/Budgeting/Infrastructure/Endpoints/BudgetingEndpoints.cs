using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Budgeting.Application.Allocations.CopyFromPeriod;
using NorthernLink.Budgeting.Application.Allocations.GetAllocations;
using NorthernLink.Budgeting.Application.Allocations.Remove;
using NorthernLink.Budgeting.Application.Allocations.Create;
using NorthernLink.Budgeting.Application.Allocations.Update;
using NorthernLink.Budgeting.Application.Codes.CopyFromPeriod;
using NorthernLink.Budgeting.Application.Codes.Create;
using NorthernLink.Budgeting.Application.Codes.Delete;
using NorthernLink.Budgeting.Application.Codes.GetCodes;
using NorthernLink.Budgeting.Application.Codes.GetOwnerCandidates;
using NorthernLink.Budgeting.Application.Codes.SeedStarterSet;
using NorthernLink.Budgeting.Application.Codes.SetActive;
using NorthernLink.Budgeting.Application.Codes.Update;
using NorthernLink.Budgeting.Application.CostCentres.Create;
using NorthernLink.Budgeting.Application.CostCentres.Delete;
using NorthernLink.Budgeting.Application.CostCentres.GetCostCentreById;
using NorthernLink.Budgeting.Application.CostCentres.GetCostCentres;
using NorthernLink.Budgeting.Application.CostCentres.GetPlannedRollup;
using NorthernLink.Budgeting.Application.CostCentres.SetActive;
using NorthernLink.Budgeting.Application.CostCentres.Update;
using NorthernLink.Budgeting.Application.Periods.Create;
using NorthernLink.Budgeting.Application.Periods.GetPeriodById;
using NorthernLink.Budgeting.Application.Periods.GetPeriods;
using NorthernLink.Budgeting.Application.Periods.Transition;
using NorthernLink.Budgeting.Application.Vendors.Create;
using NorthernLink.Budgeting.Application.Vendors.Delete;
using NorthernLink.Budgeting.Application.Vendors.GetVendorById;
using NorthernLink.Budgeting.Application.Vendors.GetVendors;
using NorthernLink.Budgeting.Application.Vendors.SetActive;
using NorthernLink.Budgeting.Application.Vendors.Update;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.CostCentres;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Budgeting.Domain.Vendors;

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

        // Periods. The four lifecycle routes are one command with a discriminator (the
        // activate/deactivate precedent below), forward-only and in this order; each answers 409
        // with the transition's own error when the period is not in the state it leaves from.
        budgeting.MapGet("periods", GetPeriods);
        budgeting.MapPost("periods", CreatePeriod);
        budgeting.MapGet("periods/{id:guid}", GetPeriod);
        budgeting.MapPost("periods/{id:guid}/finalize", FinalizePeriod);
        budgeting.MapPost("periods/{id:guid}/open", OpenPeriod);
        budgeting.MapPost("periods/{id:guid}/begin-review", BeginPeriodReview);
        budgeting.MapPost("periods/{id:guid}/close", ClosePeriod);

        // Allocations — the UI calls them budget items. A period holds any number of items per
        // code (a code's budget is the sum of its items), so items are created with POST and
        // addressed by their own id afterwards. Every write answers 409 PeriodNotEditable outside
        // Draft and Open.
        budgeting.MapGet("periods/{id:guid}/allocations", GetAllocations);
        budgeting.MapPost("periods/{id:guid}/allocations", CreateAllocation);
        budgeting.MapPut("periods/{id:guid}/allocations/{allocationId:guid}", UpdateAllocation);
        budgeting.MapDelete("periods/{id:guid}/allocations/{allocationId:guid}", RemoveAllocation);

        // Seed this period's plan from an earlier one — every field across, justifications
        // cleared (see CopyBudgetAllocationsCommandHandler). 200 with counts rather than 201, the
        // codes/starter-set precedent: it adds many items or none and has no single new resource
        // to point a Location at.
        //
        // No collision with the item routes above: POST .../allocations has one segment fewer,
        // and PUT/DELETE .../{allocationId:guid} are other verbs whose :guid constraint means the
        // literal "copy" can never bind as an id. A future POST .../allocations/{allocationId}
        // WOULD collide with this route — if one is ever added, keep the :guid constraint.
        budgeting.MapPost("periods/{id:guid}/allocations/copy", CopyAllocations);

        // Codes belong to a period: each period owns its chart, and the code STRING is the
        // cross-period identity. Every code route lives under its period, and every write answers
        // 409 Budgeting.Code.PeriodNotEditable outside Draft and Open (reads work in any state).
        // Retiring (activate/deactivate) is the normal end-of-life path: items reference codes by
        // id and by string and must keep resolving, so a code any item of its period has used is
        // retired, never deleted. DELETE exists only for a code created in error and never
        // referenced — IBudgetCodeUsageProbe turns it into a 409 otherwise.
        //
        // No collision between the literal segments (starter-set, copy) and the id routes: the
        // literals are POST-only with one segment after "codes", and every {codeId} carries the
        // :guid constraint, so "copy" can never bind as an id. Keep the constraint if a new
        // POST .../codes/{codeId} route is ever added.
        budgeting.MapGet("periods/{id:guid}/codes", GetCodes);
        budgeting.MapPost("periods/{id:guid}/codes", CreateCode);
        budgeting.MapPut("periods/{id:guid}/codes/{codeId:guid}", UpdateCode);
        budgeting.MapPost("periods/{id:guid}/codes/{codeId:guid}/activate", ActivateCode);
        budgeting.MapPost("periods/{id:guid}/codes/{codeId:guid}/deactivate", DeactivateCode);
        budgeting.MapDelete("periods/{id:guid}/codes/{codeId:guid}", DeleteCode);
        budgeting.MapPost("periods/{id:guid}/codes/starter-set", SeedStarterSet);

        // Seed this period's chart from another period's — active codes whose string this period
        // lacks, hierarchy remapped by string (see CopyBudgetCodesCommandHandler). 200 with
        // counts, the allocations/copy precedent.
        budgeting.MapPost("periods/{id:guid}/codes/copy", CopyCodes);

        // Tenant-wide by design: it lists the people who can own a code, not codes.
        budgeting.MapGet("codes/owners", GetOwnerCandidates);

        // The vendor register — tenant-wide, not per period: a vendor is the same counterparty in
        // every period. Names are unique per tenant ignoring case (409 Budgeting.Vendor.DuplicateName).
        // Retiring (activate/deactivate) is the normal end of a vendor's life; DELETE is for a
        // vendor created in error and answers 409 Budgeting.Vendor.InUse once anything references
        // it (IVendorUsageProbe — nothing can yet). GET lists active vendors unless
        // ?includeInactive=true.
        budgeting.MapGet("vendors", GetVendors);
        budgeting.MapGet("vendors/{vendorId:guid}", GetVendor);
        budgeting.MapPost("vendors", CreateVendor);
        budgeting.MapPut("vendors/{vendorId:guid}", UpdateVendor);
        budgeting.MapPost("vendors/{vendorId:guid}/activate", ActivateVendor);
        budgeting.MapPost("vendors/{vendorId:guid}/deactivate", DeactivateVendor);
        budgeting.MapDelete("vendors/{vendorId:guid}", DeleteVendor);

        // The cost-centre register — tenant-wide, NOT under a period: a cost centre is an
        // organisational unit or base, and outlives every period's chart. Budget codes carry an
        // entry's code string, validated against this register on create/edit. Retiring is the
        // normal end of life; DELETE is for an entry nothing carries (409 InUse / HasChildren
        // otherwise). Every id route keeps the :guid constraint, so no literal can bind as an id.
        budgeting.MapGet("cost-centres", GetCostCentres);
        budgeting.MapGet("cost-centres/{id:guid}", GetCostCentre);
        budgeting.MapPost("cost-centres", CreateCostCentre);
        budgeting.MapPut("cost-centres/{id:guid}", UpdateCostCentre);
        budgeting.MapPost("cost-centres/{id:guid}/activate", ActivateCostCentre);
        budgeting.MapPost("cost-centres/{id:guid}/deactivate", DeactivateCostCentre);
        budgeting.MapDelete("cost-centres/{id:guid}", DeleteCostCentre);

        // A period's planned expense per cost centre, plus a "No cost centre" bucket. Planned only
        // — actuals arrive in a later slice and the shape carries no actual field until then.
        budgeting.MapGet("periods/{id:guid}/rollups/cost-centres", GetCostCentreRollup);

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

    private static async Task<IResult> GetPeriod(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetBudgetPeriodByIdQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static Task<IResult> FinalizePeriod(
        Guid id, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken) =>
        TransitionPeriod(id, PeriodTransition.Finalize, tenantContext, currentActor, sender, cancellationToken);

    private static Task<IResult> OpenPeriod(
        Guid id, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken) =>
        TransitionPeriod(id, PeriodTransition.Open, tenantContext, currentActor, sender, cancellationToken);

    private static Task<IResult> BeginPeriodReview(
        Guid id, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken) =>
        TransitionPeriod(id, PeriodTransition.BeginReview, tenantContext, currentActor, sender, cancellationToken);

    private static Task<IResult> ClosePeriod(
        Guid id, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken) =>
        TransitionPeriod(id, PeriodTransition.Close, tenantContext, currentActor, sender, cancellationToken);

    /// <summary>The one body behind the four lifecycle routes — only the transition differs.</summary>
    private static Task<IResult> TransitionPeriod(
        Guid id,
        PeriodTransition transition,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext,
            sender,
            tenantId => new TransitionBudgetPeriodCommand(tenantId, id, transition, currentActor.UserId),
            cancellationToken);

    private static async Task<IResult> GetAllocations(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetBudgetAllocationsQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// Adds a budget item: 201 with <c>{ id }</c>. There is no single-item GET, so the Location
    /// points at the period's item list the new item now appears in.
    /// </summary>
    private static async Task<IResult> CreateAllocation(
        Guid id,
        BudgetItemRequest request,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateBudgetAllocationCommand(
            tenantId, id, request.BudgetCodeId, request.ToDetails(), currentActor.UserId);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/budgeting/periods/{id}/allocations", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    /// <summary>Rewrites a budget item (its code may change): 204.</summary>
    private static Task<IResult> UpdateAllocation(
        Guid id,
        Guid allocationId,
        BudgetItemRequest request,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext,
            sender,
            tenantId => new UpdateBudgetAllocationCommand(
                tenantId, id, allocationId, request.BudgetCodeId, request.ToDetails(), currentActor.UserId),
            cancellationToken);

    /// <summary>
    /// Copies an earlier period's plan into this one: 200 with a full account of every source
    /// line (copied / skipped because this period's code with that string is already planned /
    /// skipped because this period has no active code with that string), which always sums to
    /// <c>sourceLineCount</c>. Items land on this period's code with the same string. An empty source period
    /// is a 200 with zeroes, not an error.
    /// </summary>
    private static async Task<IResult> CopyAllocations(
        Guid id,
        CopyBudgetAllocationsRequest request,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CopyBudgetAllocationsCommand(
            tenantId, id, request.SourcePeriodId, currentActor.UserId);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Ok(new BudgetAllocationCopyResponse(
                result.Value.Copied,
                result.Value.SkippedAlreadyPlanned,
                result.Value.SkippedRetiredCode,
                result.Value.SourceLineCount))
            : EndpointResults.Problem(result.Error);
    }

    // No actor: a deleted row has nowhere to record who deleted it. See RemoveBudgetAllocationCommand.
    private static Task<IResult> RemoveAllocation(
        Guid id, Guid allocationId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext, sender, tenantId => new RemoveBudgetAllocationCommand(tenantId, id, allocationId), cancellationToken);

    private static async Task<IResult> GetCodes(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetBudgetCodesQuery(tenantId, id), cancellationToken);
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

    /// <summary>
    /// Adds a code to the period's chart: 201 with <c>{ id }</c>. There is no single-code GET, so
    /// the Location points at the period's chart the new code now appears in.
    /// </summary>
    private static async Task<IResult> CreateCode(
        Guid id,
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
            tenantId, id, request.Code ?? string.Empty, request.ToDetails(), currentActor.UserId);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/budgeting/periods/{id}/codes", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static Task<IResult> UpdateCode(
        Guid id,
        Guid codeId,
        UpdateBudgetCodeRequest request,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext,
            sender,
            tenantId => new UpdateBudgetCodeCommand(tenantId, id, codeId, request.ToDetails(), currentActor.UserId),
            cancellationToken);

    private static Task<IResult> ActivateCode(
        Guid id,
        Guid codeId,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext,
            sender,
            tenantId => new SetBudgetCodeActiveCommand(tenantId, id, codeId, true, currentActor.UserId),
            cancellationToken);

    private static Task<IResult> DeactivateCode(
        Guid id,
        Guid codeId,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext,
            sender,
            tenantId => new SetBudgetCodeActiveCommand(tenantId, id, codeId, false, currentActor.UserId),
            cancellationToken);

    // No actor: a deleted row has nowhere to record who deleted it. See DeleteBudgetCodeCommand.
    private static Task<IResult> DeleteCode(
        Guid id, Guid codeId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext, sender, tenantId => new DeleteBudgetCodeCommand(tenantId, id, codeId), cancellationToken);

    /// <summary>
    /// Creates any of the starter chart the period does not already have. 200 with a count rather
    /// than 201: it is idempotent per period, creates many rows or none, and there is no single
    /// new resource to point a Location header at.
    /// </summary>
    private static async Task<IResult> SeedStarterSet(
        Guid id,
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
            new SeedStarterBudgetCodesCommand(tenantId, id, currentActor.UserId), cancellationToken);

        return result.IsSuccess
            ? Results.Ok(new StarterSetSeededResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// Copies another period's active codes into this period's chart: 200 with a full account of
    /// every source code (copied / skipped because this period already has the string / skipped
    /// because the source code is retired), which always sums to <c>sourceCodeCount</c>. An empty
    /// source chart is a 200 with zeroes, not an error.
    /// </summary>
    private static async Task<IResult> CopyCodes(
        Guid id,
        CopyBudgetCodesRequest request,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CopyBudgetCodesCommand(tenantId, id, request.SourcePeriodId, currentActor.UserId);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Ok(new BudgetCodeCopyResponse(
                result.Value.Copied,
                result.Value.SkippedExisting,
                result.Value.SkippedRetired,
                result.Value.SourceCodeCount))
            : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetVendors(
        bool? includeInactive, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetVendorsQuery(tenantId, includeInactive ?? false), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetVendor(
        Guid vendorId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetVendorByIdQuery(tenantId, vendorId), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>Adds a vendor to the register: 201 with <c>{ id }</c> and a Location at the vendor.</summary>
    private static async Task<IResult> CreateVendor(
        VendorRequest request,
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
            new CreateVendorCommand(tenantId, request.ToDetails(), currentActor.UserId), cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/budgeting/vendors/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    /// <summary>Rewrites every editable field of a vendor (omitted optional fields are cleared): 204.</summary>
    private static Task<IResult> UpdateVendor(
        Guid vendorId,
        VendorRequest request,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext,
            sender,
            tenantId => new UpdateVendorCommand(tenantId, vendorId, request.ToDetails(), currentActor.UserId),
            cancellationToken);

    private static Task<IResult> ActivateVendor(
        Guid vendorId,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext,
            sender,
            tenantId => new SetVendorActiveCommand(tenantId, vendorId, true, currentActor.UserId),
            cancellationToken);

    private static Task<IResult> DeactivateVendor(
        Guid vendorId,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext,
            sender,
            tenantId => new SetVendorActiveCommand(tenantId, vendorId, false, currentActor.UserId),
            cancellationToken);

    // No actor: a deleted row has nowhere to record who deleted it. See DeleteVendorCommand.
    private static Task<IResult> DeleteVendor(
        Guid vendorId, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext, sender, tenantId => new DeleteVendorCommand(tenantId, vendorId), cancellationToken);

    private static async Task<IResult> GetCostCentres(
        bool? includeInactive, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(
            new GetCostCentresQuery(tenantId, includeInactive ?? false), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    private static async Task<IResult> GetCostCentre(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetCostCentreByIdQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>Adds a register entry: 201 with <c>{ id }</c>, Location the entry's own GET.</summary>
    private static async Task<IResult> CreateCostCentre(
        CostCentreRequest request,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var command = new CreateCostCentreCommand(
            tenantId, request.Code, request.ToDetails(), currentActor.UserId);

        var result = await sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/budgeting/cost-centres/{result.Value}", new EntityCreatedResponse(result.Value))
            : EndpointResults.Problem(result.Error);
    }

    private static Task<IResult> UpdateCostCentre(
        Guid id,
        CostCentreRequest request,
        ITenantContext tenantContext,
        ICurrentActor currentActor,
        ISender sender,
        CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext,
            sender,
            tenantId => new UpdateCostCentreCommand(
                tenantId, id, request.Code, request.ToDetails(), currentActor.UserId),
            cancellationToken);

    private static Task<IResult> ActivateCostCentre(
        Guid id, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext,
            sender,
            tenantId => new SetCostCentreActiveCommand(tenantId, id, true, currentActor.UserId),
            cancellationToken);

    private static Task<IResult> DeactivateCostCentre(
        Guid id, ITenantContext tenantContext, ICurrentActor currentActor, ISender sender, CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext,
            sender,
            tenantId => new SetCostCentreActiveCommand(tenantId, id, false, currentActor.UserId),
            cancellationToken);

    // No actor: a deleted row has nowhere to record who deleted it. See DeleteCostCentreCommand.
    private static Task<IResult> DeleteCostCentre(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken) =>
        SendCommand(
            tenantContext, sender, tenantId => new DeleteCostCentreCommand(tenantId, id), cancellationToken);

    private static async Task<IResult> GetCostCentreRollup(
        Guid id, ITenantContext tenantContext, ISender sender, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Query(new GetCostCentreRollupQuery(tenantId, id), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : EndpointResults.Problem(result.Error);
    }

    /// <summary>
    /// Resolve tenant → build command → dispatch → 204, for every bodiless write (code edits,
    /// period transitions, allocation removal). The command is built by a callback rather than
    /// passed in, so it can carry the tenant id the guard just proved exists.
    /// </summary>
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

/// <summary>
/// Body of POST /api/budgeting/periods/{id}/codes/starter-set. <paramref name="Created"/> counts
/// only the codes actually added to that period — zero on a re-run, which is a success.
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
/// Request body for POST /api/budgeting/periods/{id}/allocations and
/// PUT /api/budgeting/periods/{id}/allocations/{allocationId} — one budget item. The period (and,
/// on PUT, the item) come from the route, never the body.
/// <para>
/// Every field is nullable on the wire so a missing one fails as a readable domain validation
/// error (<c>CodeRequired</c>, <c>TitleRequired</c>, <c>AmountRequired</c>,
/// <c>JustificationRequired</c>…) rather than a model-binding 400 with no code in it. Send
/// <see cref="AmountCad"/> for a lump sum, or <see cref="Quantity"/> and
/// <see cref="UnitCostCad"/> together — the server then computes the amount
/// (round(q × u, 2, AwayFromZero)) and ignores any amount sent. Enums travel as their names
/// (<c>Operating|Capital</c>, <c>OneTime|Recurring</c>, <c>MustHave|ShouldHave|NiceToHave</c>);
/// they are nullable for the <see cref="CreateBudgetCodeRequest"/> reason — a non-nullable enum
/// binds an omitted property to value 0, which for Priority is MustHave rather than the
/// documented default ShouldHave.
/// </para>
/// </summary>
public sealed record BudgetItemRequest(
    Guid? BudgetCodeId,
    string? Title,
    decimal? AmountCad,
    decimal? Quantity,
    decimal? UnitCostCad,
    string? Unit,
    string? Justification,
    BudgetSpendType? SpendType,
    BudgetRecurrence? Recurrence,
    string? Vendor,
    IReadOnlyList<string>? Tags,
    BudgetItemPriority? Priority,
    string? Assumptions,
    string? ConsequenceIfUnfunded)
{
    public BudgetItemDetails ToDetails() => new()
    {
        Title = Title,
        AmountCad = AmountCad,
        Quantity = Quantity,
        UnitCostCad = UnitCostCad,
        Unit = Unit,
        Justification = Justification,
        SpendType = SpendType ?? BudgetSpendType.Operating,
        Recurrence = Recurrence ?? BudgetRecurrence.OneTime,
        Vendor = Vendor,
        Tags = Tags,
        Priority = Priority ?? BudgetItemPriority.ShouldHave,
        Assumptions = Assumptions,
        ConsequenceIfUnfunded = ConsequenceIfUnfunded,
    };
}

/// <summary>
/// Request body for POST /api/budgeting/periods/{id}/allocations/copy. The target period comes
/// from the route; this names the period to copy <em>from</em>. Nullable on the wire so an
/// omitted or null value fails as a readable <c>Budgeting.Allocation.CopySourceRequired</c>
/// rather than binding to <c>Guid.Empty</c> and reporting a not-found for the all-zeroes id.
/// </summary>
public sealed record CopyBudgetAllocationsRequest(Guid? SourcePeriodId);

/// <summary>
/// Body of a successful items copy. <paramref name="Copied"/> plus the two skip counts always
/// equals <paramref name="SourceLineCount"/> — the console reports all four so a skipped line never
/// reads as a line that vanished. Each source item lands on this period's code with the same
/// string; <paramref name="SkippedRetiredCode"/> counts items for which this period has <b>no
/// active code with that string</b> (missing or retired — the name is kept to spare churn).
/// Every copied line arrives with an <b>empty justification</b> and must be argued before it can
/// be saved again.
/// </summary>
public sealed record BudgetAllocationCopyResponse(
    int Copied,
    int SkippedAlreadyPlanned,
    int SkippedRetiredCode,
    int SourceLineCount);

/// <summary>
/// Request body for POST /api/budgeting/periods/{id}/codes/copy. The target period comes from the
/// route; this names the period to copy codes <em>from</em>. Nullable on the wire so an omitted
/// value fails as a readable <c>Budgeting.Code.CopySourceRequired</c> — the
/// <see cref="CopyBudgetAllocationsRequest"/> reasoning.
/// </summary>
public sealed record CopyBudgetCodesRequest(Guid? SourcePeriodId);

/// <summary>
/// Body of a successful codes copy. <paramref name="Copied"/> + <paramref name="SkippedExisting"/>
/// + <paramref name="SkippedRetired"/> always equals <paramref name="SourceCodeCount"/>. Copied
/// codes are active, carry every descriptive field of the source, and roll up into this period's
/// code with the source parent's string when there is one (top-level otherwise).
/// </summary>
public sealed record BudgetCodeCopyResponse(
    int Copied,
    int SkippedExisting,
    int SkippedRetired,
    int SourceCodeCount);

/// <summary>
/// Request body for POST /api/budgeting/periods/{id}/codes. The period comes from the route. Every string is nullable on the wire so a missing
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
/// Request body for PUT /api/budgeting/periods/{id}/codes/{codeId}. Carries no Code: the code string is set once
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

/// <summary>
/// Request body for POST /api/budgeting/vendors and PUT /api/budgeting/vendors/{vendorId} — the
/// same shape both ways, and PUT is a full replace: an omitted optional field is cleared. Every
/// field is nullable on the wire so a missing name fails as a readable
/// <c>Budgeting.Vendor.NameRequired</c> rather than a model-binding 400 with no code in it.
/// Strings are trimmed server-side and blank becomes null. <see cref="DefaultBudgetCode"/> is a
/// code string, normalized like one (trim + upper case) but not required to exist.
/// <see cref="GstRegistrationNumber"/> is reference data — nothing computes tax from it.
/// </summary>
public sealed record VendorRequest(
    string? Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    string? Notes,
    string? GstRegistrationNumber,
    string? QboDisplayName,
    string? DefaultBudgetCode)
{
    public VendorDetails ToDetails() => new()
    {
        Name = Name,
        ContactName = ContactName,
        Email = Email,
        Phone = Phone,
        Address = Address,
        Notes = Notes,
        GstRegistrationNumber = GstRegistrationNumber,
        QboDisplayName = QboDisplayName,
        DefaultBudgetCode = DefaultBudgetCode,
    };
}

/// <summary>
/// Request body for POST /api/budgeting/cost-centres and PUT /api/budgeting/cost-centres/{id}.
/// Every string is nullable on the wire so a missing one fails as a readable domain error.
/// <para>
/// <see cref="Code"/> is required on POST (trimmed server-side, case preserved). On PUT it is
/// optional and never written: omitted/null/blank or the entry's own code is accepted, anything
/// else is 400 <c>Budgeting.CostCentre.CodeImmutable</c> — so a client that round-trips the whole
/// record gets a clear refusal instead of a silently ignored rename.
/// </para>
/// </summary>
public sealed record CostCentreRequest(
    string? Code,
    string? Name,
    string? Description,
    Guid? OwnerUserId,
    Guid? ParentId)
{
    public CostCentreDetails ToDetails() => new()
    {
        Name = Name ?? string.Empty,
        Description = Description,
        OwnerUserId = OwnerUserId,
        ParentId = ParentId,
    };
}
