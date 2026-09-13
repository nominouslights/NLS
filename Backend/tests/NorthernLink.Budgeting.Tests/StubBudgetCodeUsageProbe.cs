using NorthernLink.Budgeting.Application.Abstractions;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// Usage probe whose answer the test chooses. The shipped implementation
/// (<c>AllocationBudgetCodeUsageProbe</c>) answers from allocation lines; this stub keeps the
/// delete handler's tests about the handler — guard order, save-on-refusal — instead of about
/// building lines, and records what it was asked so the id-and-string contract is pinned.
/// <see cref="AllocationBudgetCodeUsageProbeTests"/> wires the real probe over the in-memory
/// allocation repository for the end-to-end refusal.
/// </summary>
internal sealed class StubBudgetCodeUsageProbe : IBudgetCodeUsageProbe
{
    public bool Referenced { get; set; }

    public Guid? LastProbedId { get; private set; }

    public string? LastProbedCode { get; private set; }

    public Task<bool> IsReferencedAsync(
        Guid budgetCodeId, string code, CancellationToken cancellationToken = default)
    {
        LastProbedId = budgetCodeId;
        LastProbedCode = code;
        return Task.FromResult(Referenced);
    }
}
