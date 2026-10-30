namespace NorthernLink.Budgeting.Application.Allocations;

/// <summary>
/// What <c>CopyBudgetAllocationsCommand</c> returns: a full account of what happened to every
/// line of the source period's plan.
/// <para>
/// <b><paramref name="Copied"/> + <paramref name="SkippedAlreadyPlanned"/> +
/// <paramref name="SkippedRetiredCode"/> == <paramref name="SourceLineCount"/>, always.</b> The
/// copy skips rather than fails (the starter-set precedent), so the console has to be able to
/// show the user that nothing went missing — a skip nobody counted reads as data loss.
/// </para>
/// <para>
/// <paramref name="SkippedAlreadyPlanned"/> is the count of source lines whose budget code — the
/// target period's code with the same string — the target already plans (at least one item on
/// that code before the copy ran). Those are
/// never copied, so a second copy is a no-op; the existing items carry a
/// justification somebody wrote, and destroying it is the opposite of what a "start from last
/// period" button is for.
/// </para>
/// <para>
/// <paramref name="SkippedRetiredCode"/> counts source lines for which the target period has
/// <b>no active code with that string</b> — the string is missing from the target's chart, or
/// present but retired. From the planner's side both are "that code isn't on offer in this
/// period". The field keeps its original name to spare the console a rename.
/// </para>
/// <para>
/// All four zero is a success, not a failure: an empty source period copies nothing, and the
/// console says "nothing to copy" rather than raising an error for a button that worked.
/// </para>
/// </summary>
public sealed record BudgetAllocationCopyResult(
    int Copied,
    int SkippedAlreadyPlanned,
    int SkippedRetiredCode,
    int SourceLineCount);
