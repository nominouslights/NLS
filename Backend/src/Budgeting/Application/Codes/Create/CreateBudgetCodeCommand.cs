using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Codes.Create;

/// <summary>
/// Creates a new (active) budget code. Returns the new code's id.
/// <para>
/// <paramref name="ActorId"/> comes from the endpoint's <c>ICurrentActor</c> — the signed
/// token's <c>sub</c> claim — never from the request body, so <c>created_by</c> cannot be forged
/// by the caller creating the row.
/// </para>
/// </summary>
public sealed record CreateBudgetCodeCommand(
    Guid TenantId,
    string Code,
    BudgetCodeDetails Details,
    Guid? ActorId) : ICommand<Guid>;
