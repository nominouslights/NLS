using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Tests;

/// <summary>In-memory fake of the write-side code repository for handler tests.</summary>
internal sealed class InMemoryBudgetCodeRepository : IBudgetCodeRepository
{
    public List<BudgetCode> Codes { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<BudgetCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Codes.FirstOrDefault(c => c.Id == id));

    // Ordinal, matching the real repository: the handler normalizes before calling, so a
    // case-insensitive comparison here would hide a missing normalization rather than paper over it.
    public Task<BudgetCode?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(Codes.FirstOrDefault(c => string.Equals(c.Code, normalizedCode, StringComparison.Ordinal)));

    public Task<bool> HasChildrenAsync(Guid parentCodeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Codes.Any(c => c.ParentCodeId == parentCodeId));

    public void Add(BudgetCode budgetCode) => Codes.Add(budgetCode);

    public void Remove(BudgetCode budgetCode) => Codes.Remove(budgetCode);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
