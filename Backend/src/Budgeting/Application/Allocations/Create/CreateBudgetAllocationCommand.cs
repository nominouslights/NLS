using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Domain.Allocations;

namespace NorthernLink.Budgeting.Application.Allocations.Create;

/// <summary>
/// Adds one budget item to a period's plan against <paramref name="BudgetCodeId"/>. A period may
/// hold any number of items per code — a code's budget is the sum of its items — so this always
/// creates; there is no upsert. Returns the new item's id.
/// <para>
/// <paramref name="BudgetCodeId"/> is nullable so an omitted code fails as a readable
/// <c>CodeRequired</c> rather than a not-found for <c>Guid.Empty</c>; <paramref name="Details"/>
/// is raw input, validated by <c>BudgetAllocation.Validate</c> before any lookup.
/// <paramref name="ActorId"/> comes from the endpoint's <c>ICurrentActor</c> — the signed token's
/// <c>sub</c> claim — never from the request body.
/// </para>
/// </summary>
public sealed record CreateBudgetAllocationCommand(
    Guid TenantId,
    Guid PeriodId,
    Guid? BudgetCodeId,
    BudgetItemDetails Details,
    Guid? ActorId) : ICommand<Guid>;
