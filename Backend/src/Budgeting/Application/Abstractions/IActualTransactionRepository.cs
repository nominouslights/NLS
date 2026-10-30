using NorthernLink.Budgeting.Domain.Actuals;

namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>Write-side persistence for the ActualTransaction aggregate (tenant-scoped).</summary>
public interface IActualTransactionRepository
{
    /// <summary>
    /// The tenant's transaction with that id, or null. Transactions are reached by their own id,
    /// not by a pair — unlike an allocation line, two spends on the same code on the same day are
    /// two ordinary, entirely valid rows.
    /// </summary>
    Task<ActualTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether any transaction <b>dated inside the code's period</b> references the code — by id
    /// <b>or</b> by string. Serves <see cref="IBudgetCodeUsageProbe"/> and the retirement rule
    /// alongside the allocation half: a code deleted and recreated under the same string has a new
    /// id, and its old rows must still count as references.
    /// <para>
    /// <b>Scoped by date, because codes belong to a period and a transaction stores none.</b> The
    /// string is the cross-period identity, so an unscoped match would let Q3's spend on its FUEL
    /// pin Q4's unused FUEL. A transaction counts in the period covering its date — the same
    /// derivation every other read makes — so the caller passes that period's
    /// <paramref name="startsOn"/>/<paramref name="endsOn"/>, both inclusive.
    /// </para>
    /// </summary>
    Task<bool> ExistsForCodeAsync(
        Guid budgetCodeId,
        string code,
        DateOnly startsOn,
        DateOnly endsOn,
        CancellationToken cancellationToken = default);

    void Add(ActualTransaction transaction);

    /// <summary>
    /// Hard delete — a transaction recorded in error is not history, it is a mistake; the journal
    /// keeps its final snapshot.
    /// </summary>
    void Remove(ActualTransaction transaction);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
