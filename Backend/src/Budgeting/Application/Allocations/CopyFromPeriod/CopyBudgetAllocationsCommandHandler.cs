using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Allocations.CopyFromPeriod;

/// <summary>
/// Handles <see cref="CopyBudgetAllocationsCommand"/>. A copy is N sets in a trench coat, so the
/// order of the guards mirrors <c>SetBudgetAllocationCommandHandler</c>'s and is equally the
/// contract:
/// <list type="number">
/// <item><b>Validate the input</b> before any lookup — a source must be named
/// (<see cref="BudgetAllocationErrors.CopySourceRequired"/>) and must not be the target
/// (<see cref="BudgetAllocationErrors.CopySourceIsTarget"/>). Self-copy is refused rather than
/// allowed to succeed: it would otherwise return a baffling 200 that skipped every line as
/// "already planned".</item>
/// <item><b>The target period exists</b> (tenant-filtered, so another tenant's id reads as
/// NotFound) <b>and allows plan changes</b> — Draft or Open; anything else is
/// <see cref="BudgetAllocationErrors.PeriodNotEditable"/> for the whole request. Byte-for-byte
/// the set handler's guard, and checked <em>before</em> the source on purpose: the target is the
/// resource the route addresses and the only place a row will land.</item>
/// <item><b>The source period exists</b> (tenant-filtered) →
/// <see cref="BudgetAllocationErrors.CopySourceNotFound"/>, its own error rather than
/// <c>BudgetPeriodErrors.NotFound</c> because two period ids are in play and the console must be
/// able to say which one was wrong.
/// <b>There is deliberately NO editability check on the source</b> — copying a Closed period's
/// plan into a fresh Draft is the entire point of the feature, and "the source must be editable"
/// would refuse the single most common case. Nothing is written to the source, so
/// <c>AllowsPlanChanges</c> has no bearing on it. Do not add that check.</item>
/// <item><b>Copy line by line, skipping rather than failing</b> (the
/// <c>SeedStarterBudgetCodesCommandHandler</c> precedent). One save, and only if something was
/// actually copied — a copy that skips everything writes nothing at all.</item>
/// </list>
/// <para>
/// The three skips, in the order they are tested per line:
/// <b>already planned</b> (the target has a line on that code — skipped and never overwritten,
/// because rewriting it would destroy a justification somebody wrote and fight the unique
/// (tenant, period, code) index), then <b>retired or missing code</b> (mirroring the set
/// handler's <c>CodeRetired</c> guard; a code gone from the chart entirely folds in here, since
/// from the planner's side both mean "not on offer any more"). Already-planned is tested first
/// so a line that is both reports the reason that actually protects something.
/// </para>
/// <para>
/// A zero-amount source line is copied as-is — zero is a valid plan — and an empty source period
/// is a success with all four counts zero, not an error.
/// </para>
/// </summary>
public sealed class CopyBudgetAllocationsCommandHandler(
    IBudgetAllocationRepository allocations,
    IBudgetPeriodRepository periods,
    IBudgetCodeRepository codes)
    : ICommandHandler<CopyBudgetAllocationsCommand, BudgetAllocationCopyResult>
{
    public async Task<Result<BudgetAllocationCopyResult>> Handle(
        CopyBudgetAllocationsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SourcePeriodId is not { } sourcePeriodId)
        {
            return Result.Failure<BudgetAllocationCopyResult>(BudgetAllocationErrors.CopySourceRequired);
        }

        if (sourcePeriodId == command.PeriodId)
        {
            return Result.Failure<BudgetAllocationCopyResult>(BudgetAllocationErrors.CopySourceIsTarget);
        }

        var target = await periods.GetByIdAsync(command.PeriodId, cancellationToken);
        if (target is null)
        {
            return Result.Failure<BudgetAllocationCopyResult>(BudgetPeriodErrors.NotFound);
        }

        if (!target.AllowsPlanChanges)
        {
            return Result.Failure<BudgetAllocationCopyResult>(BudgetAllocationErrors.PeriodNotEditable);
        }

        // No AllowsPlanChanges check here — see the class comment. A Closed source is legal.
        var source = await periods.GetByIdAsync(sourcePeriodId, cancellationToken);
        if (source is null)
        {
            return Result.Failure<BudgetAllocationCopyResult>(BudgetAllocationErrors.CopySourceNotFound);
        }

        var sourceLines = await allocations.ListForPeriodAsync(sourcePeriodId, cancellationToken);
        if (sourceLines.Count == 0)
        {
            // Success with zeroes, so the console can say "nothing to copy" rather than raise an
            // error for a button that worked. No further reads and no save.
            return Result.Success(new BudgetAllocationCopyResult(0, 0, 0, 0));
        }

        var targetLines = await allocations.ListForPeriodAsync(command.PeriodId, cancellationToken);
        var alreadyPlanned = targetLines.Select(line => line.BudgetCodeId).ToHashSet();

        // One load of the chart rather than a GetByIdAsync per source line. A code missing from
        // the set is one deleted outright, and lands in the same bucket as a retired one.
        var chart = await codes.GetAllAsync(cancellationToken);
        var liveCodeIds = chart.Where(code => code.IsActive).Select(code => code.Id).ToHashSet();

        var copied = 0;
        var skippedAlreadyPlanned = 0;
        var skippedRetiredCode = 0;

        foreach (var line in sourceLines)
        {
            if (alreadyPlanned.Contains(line.BudgetCodeId))
            {
                skippedAlreadyPlanned++;
                continue;
            }

            if (!liveCodeIds.Contains(line.BudgetCodeId))
            {
                skippedRetiredCode++;
                continue;
            }

            allocations.Add(line.CopyInto(command.PeriodId, command.ActorId));
            copied++;
        }

        if (copied > 0)
        {
            await allocations.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new BudgetAllocationCopyResult(
            copied, skippedAlreadyPlanned, skippedRetiredCode, sourceLines.Count));
    }
}
