using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Actuals;

namespace NorthernLink.Budgeting.Tests;

/// <summary>In-memory fake of the write-side actuals repository for handler tests.</summary>
internal sealed class InMemoryActualTransactionRepository : IActualTransactionRepository
{
    public List<ActualTransaction> Transactions { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<ActualTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Transactions.FirstOrDefault(t => t.Id == id));

    // Id OR string, like the real repository's `t.BudgetCodeId == budgetCodeId || t.Code == code`,
    // bounded to the period's dates inclusive at both ends. The string half is ordinal because a
    // Postgres varchar equality is: a case-insensitive match here would make the probe look more
    // forgiving than the database it stands in for.
    public Task<bool> ExistsForCodeAsync(
        Guid budgetCodeId,
        string code,
        DateOnly startsOn,
        DateOnly endsOn,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Transactions.Any(t =>
            (t.BudgetCodeId == budgetCodeId || string.Equals(t.Code, code, StringComparison.Ordinal))
            && t.OccurredOn >= startsOn
            && t.OccurredOn <= endsOn));

    public void Add(ActualTransaction transaction) => Transactions.Add(transaction);

    public void Remove(ActualTransaction transaction) => Transactions.Remove(transaction);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
