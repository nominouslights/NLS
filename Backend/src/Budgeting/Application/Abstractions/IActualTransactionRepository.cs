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
    /// Whether any transaction references the code — by id <b>or</b> by string. Serves
    /// <see cref="IBudgetCodeUsageProbe"/> alongside the allocation half: a code deleted and
    /// recreated under the same string has a new id, and its old rows must still count as
    /// references.
    /// </summary>
    Task<bool> ExistsForCodeAsync(Guid budgetCodeId, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// The distinct dates on which anything was recorded against the code — by id <b>or</b> by
    /// string, the same match <see cref="ExistsForCodeAsync"/> makes.
    /// <para>
    /// <b>Dates, not period ids</b>, because a transaction stores no period: the caller maps each
    /// date onto the period covering it. That is the same derivation every other read makes, so
    /// the retirement rule and the period view can never disagree about which period a
    /// transaction belongs to.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<DateOnly>> GetDatesForCodeAsync(
        Guid budgetCodeId, string code, CancellationToken cancellationToken = default);

    void Add(ActualTransaction transaction);

    /// <summary>
    /// Hard delete — a transaction recorded in error is not history, it is a mistake; the journal
    /// keeps its final snapshot.
    /// </summary>
    void Remove(ActualTransaction transaction);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
