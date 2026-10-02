namespace NorthernLink.Budgeting.Application.Codes;

/// <summary>
/// What <c>CopyBudgetCodesCommand</c> returns: a full account of every source code.
/// <b><paramref name="Copied"/> + <paramref name="SkippedExisting"/> +
/// <paramref name="SkippedRetired"/> == <paramref name="SourceCodeCount"/>, always</b> — a skip
/// nobody counted reads as data loss.
/// <para>
/// <paramref name="SkippedRetired"/>: the source code is retired, and retired codes are never
/// copied (a retired code is the decision "not next period"). It is tested first, so a retired
/// source code whose string the target also has reports as retired.
/// <paramref name="SkippedExisting"/>: the target already has a code with that string — active
/// <em>or</em> retired. A retired target code is a decision somebody made in this period, and a
/// copy that resurrected it (or collided with it on the unique index) would undo it.
/// </para>
/// </summary>
public sealed record BudgetCodeCopyResult(
    int Copied,
    int SkippedExisting,
    int SkippedRetired,
    int SourceCodeCount);
