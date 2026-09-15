namespace NorthernLink.Budgeting.Application.Abstractions;

/// <summary>
/// Answers the one question a hard delete must ask: has anything ever been tagged with this
/// budget code? A code that has been used is retired, never deleted — historical allocations and
/// actual transactions must keep resolving to a code that still exists.
/// <para>
/// It takes <b>both</b> the id and the code string on purpose. An allocation line carries the
/// code's id <em>and</em> an immutable copy of its string, and the probe matches on either: a
/// code deleted and recreated under the same string has a new id, and its old lines must still
/// count. When actual transactions arrive they plug into the same implementation
/// (<c>AllocationBudgetCodeUsageProbe</c>) without touching this interface or its caller.
/// </para>
/// </summary>
public interface IBudgetCodeUsageProbe
{
    Task<bool> IsReferencedAsync(Guid budgetCodeId, string code, CancellationToken cancellationToken = default);
}
