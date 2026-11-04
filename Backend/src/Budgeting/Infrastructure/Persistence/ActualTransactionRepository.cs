using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Actuals;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="BudgetingDbContext"/> (tenant-filtered).</summary>
internal sealed class ActualTransactionRepository(BudgetingDbContext context) : IActualTransactionRepository
{
    public Task<ActualTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.ActualTransactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    // Over the write table, not the read model: the delete-code and retire-code paths ask this in
    // the same request that would remove or retire the code, and the projection may still be a
    // poll behind. The id arm is a seek on (tenant_id, budget_code_id); the string arm is the rare
    // recreated-code case and scans the tenant's rows, which is the trade the index comment
    // records. The date bounds are the code's period, inclusive at both ends like Covers.
    public Task<bool> ExistsForCodeAsync(
        Guid budgetCodeId,
        string code,
        DateOnly startsOn,
        DateOnly endsOn,
        CancellationToken cancellationToken = default) =>
        context.ActualTransactions.AnyAsync(
            t => (t.BudgetCodeId == budgetCodeId || t.Code == code)
                && t.OccurredOn >= startsOn
                && t.OccurredOn <= endsOn,
            cancellationToken);

    public void Add(ActualTransaction transaction) => context.ActualTransactions.Add(transaction);

    public void Remove(ActualTransaction transaction) => context.ActualTransactions.Remove(transaction);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
