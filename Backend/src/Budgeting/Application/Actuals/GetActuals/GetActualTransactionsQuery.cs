using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Actuals.GetActuals;

/// <summary>
/// Lists one period's recorded transactions, ordered by date then code. NotFound when the period
/// does not exist. The period is named by id here and resolved to its dates by the handler,
/// because a transaction stores no period id — see <c>ActualTransaction</c>.
/// </summary>
public sealed record GetActualTransactionsQuery(Guid TenantId, Guid PeriodId)
    : IQuery<IReadOnlyList<ActualTransactionResponse>>;
