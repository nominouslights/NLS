using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Allocations.Set;

/// <summary>
/// Sets the plan for one budget code in one period — an upsert by (period, code): the first
/// call creates the line, every later call rewrites its amount and justification in place.
/// Returns the line's id and whether this call created it.
/// <para>
/// <paramref name="AmountCad"/> and <paramref name="Justification"/> are nullable so a missing
/// field fails as a readable domain validation error rather than a model-binding 400 with no
/// code in it. <paramref name="ActorId"/> comes from the endpoint's <c>ICurrentActor</c> — the
/// signed token's <c>sub</c> claim — never from the request body.
/// </para>
/// </summary>
public sealed record SetBudgetAllocationCommand(
    Guid TenantId,
    Guid PeriodId,
    Guid BudgetCodeId,
    decimal? AmountCad,
    string? Justification,
    Guid? ActorId) : ICommand<BudgetAllocationSetResult>;
