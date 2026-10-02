using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Codes.Create;

/// <summary>
/// Creates a new (active) budget code in <paramref name="PeriodId"/>'s chart. Returns the new
/// code's id.
/// <para>
/// <paramref name="ActorId"/> comes from the endpoint's <c>ICurrentActor</c> — the signed
/// token's <c>sub</c> claim — never from the request body, so <c>created_by</c> cannot be forged
/// by the caller creating the row. The period comes from the route, never the body.
/// </para>
/// </summary>
public sealed record CreateBudgetCodeCommand(
    Guid TenantId,
    Guid PeriodId,
    string Code,
    BudgetCodeDetails Details,
    Guid? ActorId) : ICommand<Guid>;
