using NorthernLink.Budgeting.Application.Abstractions;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// Usage probe whose answer the test chooses. The shipped implementation always reports "never
/// used" because nothing references a budget code yet, so without this stub the delete handler's
/// refusal path would be untestable until Stage 6.2 — and an untested refusal is how a guard
/// ships broken.
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
