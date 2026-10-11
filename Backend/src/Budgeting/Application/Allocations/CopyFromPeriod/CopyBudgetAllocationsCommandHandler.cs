using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Allocations.CopyFromPeriod;

/// <summary>
/// Handles <see cref="CopyBudgetAllocationsCommand"/>. A copy is N sets in a trench coat, so the
/// order of the guards mirrors <c>CreateBudgetAllocationCommandHandler</c>'s and is equally the
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
/// the create handler's guard, and checked <em>before</em> the source on purpose: the target is the
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
/// <b>Codes are matched by string.</b> Each period owns its own chart, so a source item's code id
/// names a code of the <em>source</em> period; the copy lands on the <em>target</em> period's code
/// with the same string (the string is the cross-period identity). The source item's own
/// <c>Code</c> copy is the string used — it is immutable and always equals its code's string.
/// </para>
/// <para>
/// The skips, in the order they are tested per line:
/// <b>already planned</b> (the target's code with that string already has at least one item —
/// skipped and never overwritten or added to, because a planner who has started on a code in this
/// period has made their own decisions there, and because it is what makes running the copy twice
/// a no-op), then <b>no active code with that string in this period</b> (the target chart lacks
/// the string entirely, or has it retired — mirroring the create handler's <c>CodeRetired</c>
/// guard; from the planner's side both mean "not on offer here"). Already-planned is tested first
/// so a line that is both reports the reason that actually protects something. The second bucket
/// keeps its wire name <c>skippedRetiredCode</c> to spare the console churn.
/// </para>
/// <para>
/// A zero-amount source line is copied as-is — zero is a valid plan — and an empty source period
/// is a success with all four counts zero, not an error.
/// </para>
/// <para>
/// <b>Vendor shares come across as-is</b> onto a target code that is an expense code (vendors are
/// tenant-wide, so the same ids are valid in every period; a share on a since-retired vendor is
/// carried too), and are dropped onto a revenue code — <c>BudgetAllocation.CopyInto</c> decides
/// by the <em>target</em> code's category. Because a copy can make a vendor referenced by one
/// more item, a copy that carries shares runs its save under the tenant's vendor register lock.
/// </para>
/// <para>
/// <b>The lock is decided by the plan, not by the source.</b> It is taken only when at least one
/// <em>planned</em> copy carries a share — so a source whose share-bearing items are all skipped
/// (already planned, code not on offer) or land on a revenue code (shares dropped) never
/// serializes with the vendor register, nor does a plan with no shares at all. (The chart copy
/// cannot do this cheaply and locks always; see <c>CopyBudgetCodesCommandHandler</c>.)
/// Because the source is read <em>before</em> the lock, a carried vendor may have been deleted in
/// between (its share removed from the source elsewhere, so the delete's probe passed).
/// Re-reading after locking would hand back EF's already-tracked instances, so instead the handler
/// confirms under the lock, in one query, that every carried vendor still exists. Under the lock
/// that answer holds until the commit — a vendor delete waits and then sees the copies; a vendor
/// deleted in the gap fails the whole copy with <see cref="BudgetAllocationErrors.CopySourceChanged"/>
/// (409, nothing written — run it again). Retired vendors still exist, so they are carried.
/// </para>
/// </summary>
public sealed class CopyBudgetAllocationsCommandHandler(
    IBudgetAllocationRepository allocations,
    IBudgetPeriodRepository periods,
    IBudgetCodeRepository codes,
    IVendorRepository vendors)
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

        var plan = await PlanAsync(command, sourceLines, cancellationToken);
        if (plan.Copies.Count == 0)
        {
            // A copy that skips everything writes nothing at all.
            return Result.Success(plan.ToResult(sourceLines.Count));
        }

        // Released (rolled back) on every early return, committed with the save. Taken only when
        // a planned copy actually carries a share — see the class comment.
        IVendorRegisterLock? vendorLock = null;
        try
        {
            var carried = plan.Copies
                .SelectMany(copy => copy.VendorShares)
                .Select(share => share.VendorId)
                .Distinct()
                .ToList();
            if (carried.Count > 0)
            {
                vendorLock = await vendors.LockRegisterAsync(command.TenantId, cancellationToken);

                // The source was read before the lock, so a carried vendor may have been deleted
                // since (its share removed elsewhere first, letting the delete's probe pass).
                // Confirm under the lock, in one query, that every carried vendor still exists —
                // from here a vendor delete waits for this commit and then sees the copies.
                var existing = await vendors.GetByIdsAsync(carried, cancellationToken);
                if (existing.Count != carried.Count)
                {
                    return Result.Failure<BudgetAllocationCopyResult>(BudgetAllocationErrors.CopySourceChanged);
                }
            }

            foreach (var copy in plan.Copies)
            {
                allocations.Add(copy);
            }

            await allocations.SaveChangesAsync(cancellationToken);
            if (vendorLock is not null)
            {
                await vendorLock.CommitAsync(cancellationToken);
            }

            return Result.Success(plan.ToResult(sourceLines.Count));
        }
        finally
        {
            if (vendorLock is not null)
            {
                await vendorLock.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Decides every source line — copy or which skip — without adding anything, so the handler
    /// can still refuse the whole copy before the unit of work holds a single new item.
    /// </summary>
    private async Task<CopyPlan> PlanAsync(
        CopyBudgetAllocationsCommand command,
        IReadOnlyList<BudgetAllocation> sourceLines,
        CancellationToken cancellationToken)
    {
        // Snapshotted BEFORE the loop and never updated inside it: "already planned" means planned
        // in the target before this copy ran. Adding each copied item's code to the set would skip
        // the second and later source items of a code whose first item was just copied — many
        // items per code is the model, and every one of them must come across. Counts stay per
        // ITEM: copied + skippedAlreadyPlanned + skippedRetiredCode == sourceLineCount.
        var targetLines = await allocations.ListForPeriodAsync(command.PeriodId, cancellationToken);
        var alreadyPlanned = targetLines.Select(line => line.BudgetCodeId).ToHashSet();

        // One load of the TARGET period's chart rather than a lookup per source line, keyed by
        // string — the cross-period identity. The source chart is never consulted: whether a
        // code is on offer is a question about the period the item is landing in.
        var targetChart = await codes.ListForPeriodAsync(command.PeriodId, cancellationToken);
        var targetByCode = targetChart.ToDictionary(code => code.Code, StringComparer.Ordinal);

        var plan = new CopyPlan();
        foreach (var line in sourceLines)
        {
            var targetCode = targetByCode.GetValueOrDefault(line.Code);

            if (targetCode is not null && alreadyPlanned.Contains(targetCode.Id))
            {
                plan.SkippedAlreadyPlanned++;
                continue;
            }

            if (targetCode is not { IsActive: true })
            {
                plan.SkippedRetiredCode++;
                continue;
            }

            plan.Copies.Add(line.CopyInto(command.PeriodId, targetCode.Id, targetCode.Category, command.ActorId));
        }

        return plan;
    }

    private sealed class CopyPlan
    {
        public List<BudgetAllocation> Copies { get; } = [];

        public int SkippedAlreadyPlanned { get; set; }

        public int SkippedRetiredCode { get; set; }

        public BudgetAllocationCopyResult ToResult(int sourceLineCount) =>
            new(Copies.Count, SkippedAlreadyPlanned, SkippedRetiredCode, sourceLineCount);
    }
}
