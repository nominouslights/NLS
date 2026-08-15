namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Answers the one question a hard delete must ask: has anything ever been tagged with this
/// budget code? A code that has been used is retired, never deleted — historical allocations and
/// actual transactions must keep resolving to a code that still exists.
/// <para>
/// It takes <b>both</b> the id and the code string on purpose. Allocations reference a code by
/// its string (that is what a person reads and types, and why the string is immutable), while a
/// future table may well carry the id instead. Passing both now means Stage 6.2 replaces the
/// implementation without touching this interface or any caller.
/// </para>
/// </summary>
public interface IBudgetCodeUsageProbe
{
    Task<bool> IsReferencedAsync(Guid budgetCodeId, string code, CancellationToken cancellationToken = default);
}
