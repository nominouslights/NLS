using Microsoft.EntityFrameworkCore;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Actuals;

namespace NorthernLink.Budgeting.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="BudgetingDbContext"/> (tenant-filtered).</summary>
internal sealed class ActualTransactionRepository(BudgetingDbContext context) : IActualTransactionRepository
{
    public Task<ActualTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.ActualTransactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    // Over the write table, not the read model: the delete-code path asks this in the same
    // request that would remove the code, and the projection may still be a poll behind. The id
    // arm is a seek on (tenant_id, budget_code_id); the string arm is the rare recreated-code
    // case and scans the tenant's rows, which is the trade the index comment records.
    public Task<bool> ExistsForCodeAsync(
        Guid budgetCodeId, string code, CancellationToken cancellationToken = default) =>
        context.ActualTransactions.AnyAsync(
            t => t.BudgetCodeId == budgetCodeId || t.Code == code, cancellationToken);

    // Same id-or-string match, and over the write table for the same reason. Distinct dates
    // rather than rows: a code with a thousand fuel purchases in one period still only names one
    // period in the refusal message.
    public async Task<IReadOnlyList<DateOnly>> GetDatesForCodeAsync(
        Guid budgetCodeId, string code, CancellationToken cancellationToken = default) =>
        await context.ActualTransactions
            .Where(t => t.BudgetCodeId == budgetCodeId || t.Code == code)
            .Select(t => t.OccurredOn)
            .Distinct()
            .ToListAsync(cancellationToken);

    public void Add(ActualTransaction transaction) => context.ActualTransactions.Add(transaction);

    public void Remove(ActualTransaction transaction) => context.ActualTransactions.Remove(transaction);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
