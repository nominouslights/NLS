using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Allocations.CopyFromPeriod;

/// <summary>
/// Seeds one period's plan from an earlier one: every line of
/// <paramref name="SourcePeriodId"/>'s plan is re-created in <paramref name="PeriodId"/> with the
/// same budget code and the same amount, and <b>with no justification at all</b>. Zero-based
/// budgeting's fifth step is "make a new budget before the month begins", and rebuilding a dozen
/// lines by hand is why people abandon it — but the amount is a starting position, not an
/// argument, so each copied line has to be argued again before it can be saved. See
/// <c>BudgetAllocation.CopyInto</c>.
/// <para>
/// <paramref name="SourcePeriodId"/> is nullable so an omitted field fails as a readable domain
/// validation error (<c>CopySourceRequired</c>) rather than a model-binding 400 with no code in
/// it — the same reasoning as <c>SetBudgetAllocationCommand</c>'s nullable amount.
/// <paramref name="ActorId"/> comes from the endpoint's <c>ICurrentActor</c> — the signed token's
/// <c>sub</c> claim — never from the request body; it lands as <c>created_by</c> on every copied
/// line, because whoever ran the copy is who put those numbers there.
/// </para>
/// <para>
/// Nothing is ever overwritten and nothing is removed: this only ever adds lines the target does
/// not have. Running it twice is safe — the second run copies nothing.
/// </para>
/// </summary>
public sealed record CopyBudgetAllocationsCommand(
    Guid TenantId,
    Guid PeriodId,
    Guid? SourcePeriodId,
    Guid? ActorId) : ICommand<BudgetAllocationCopyResult>;
