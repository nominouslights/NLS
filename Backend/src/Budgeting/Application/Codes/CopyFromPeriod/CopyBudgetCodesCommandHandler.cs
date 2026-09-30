using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;

namespace NorthernLink.Budgeting.Application.Codes.CopyFromPeriod;

/// <summary>
/// Handles <see cref="CopyBudgetCodesCommand"/>. The guard order mirrors
/// <c>CopyBudgetAllocationsCommandHandler</c>'s and is equally the contract:
/// <list type="number">
/// <item><b>Validate the input</b> before any lookup — a source must be named
/// (<see cref="BudgetCodeErrors.CopySourceRequired"/>) and must not be the target
/// (<see cref="BudgetCodeErrors.CopySourceIsTarget"/>).</item>
/// <item><b>The target period exists and is Draft or Open</b> (<see cref="BudgetPeriodErrors.NotFound"/>
/// / <see cref="BudgetCodeErrors.PeriodNotEditable"/>) — checked before the source because the
/// target is the resource the route addresses and the only place a row will land.</item>
/// <item><b>The source period exists</b> → <see cref="BudgetCodeErrors.CopySourceNotFound"/>.
/// <b>Deliberately no editability check on the source</b> — copying a Closed period's chart into
/// a fresh Draft is the common case, and nothing is written to the source.</item>
/// <item><b>Copy code by code, skipping rather than failing</b>; one save, and only if something
/// was copied.</item>
/// </list>
/// <para>
/// <b>Hierarchy.</b> Parents are copied before children (top-level source codes first), so by
/// the time a child is copied its parent's copy already exists in the working set. A copied
/// child's parent is the <em>target</em> code carrying the source parent's string — just copied,
/// or already in the target — because the source parent's id names a code of the source period.
/// When the target has no code with that string (the source parent was retired and skipped), the
/// child is copied top-level rather than failing. The one-level rule is never broken: a target
/// code that already rolls up into another cannot become a parent, and the child is then copied
/// top-level too.
/// </para>
/// </summary>
public sealed class CopyBudgetCodesCommandHandler(
    IBudgetCodeRepository codes,
    IBudgetPeriodRepository periods)
    : ICommandHandler<CopyBudgetCodesCommand, BudgetCodeCopyResult>
{
    public async Task<Result<BudgetCodeCopyResult>> Handle(
        CopyBudgetCodesCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SourcePeriodId is not { } sourcePeriodId)
        {
            return Result.Failure<BudgetCodeCopyResult>(BudgetCodeErrors.CopySourceRequired);
        }

        if (sourcePeriodId == command.PeriodId)
        {
            return Result.Failure<BudgetCodeCopyResult>(BudgetCodeErrors.CopySourceIsTarget);
        }

        var targetResult = await BudgetCodePeriodRule.RequireEditableAsync(periods, command.PeriodId, cancellationToken);
        if (targetResult.IsFailure)
        {
            return Result.Failure<BudgetCodeCopyResult>(targetResult.Error);
        }

        // No AllowsPlanChanges check here — see the class comment. A Closed source is legal.
        if (await periods.GetByIdAsync(sourcePeriodId, cancellationToken) is null)
        {
            return Result.Failure<BudgetCodeCopyResult>(BudgetCodeErrors.CopySourceNotFound);
        }

        var sourceCodes = await codes.ListForPeriodAsync(sourcePeriodId, cancellationToken);
        if (sourceCodes.Count == 0)
        {
            return Result.Success(new BudgetCodeCopyResult(0, 0, 0, 0));
        }

        var targetCodes = await codes.ListForPeriodAsync(command.PeriodId, cancellationToken);

        // Every string the target has, active or retired. Updated as codes are copied, so a child
        // copied later in this run finds its just-copied parent here.
        var targetByCode = targetCodes.ToDictionary(code => code.Code, StringComparer.Ordinal);

        // The source parent is looked up by id among the source period's own codes — the only
        // place its id means anything.
        var sourceById = sourceCodes.ToDictionary(code => code.Id);

        // Parents before children; ordinal code order within each tier so the run is
        // deterministic. A code whose parent id resolves to nothing in the source (a dangling
        // id written before the delete guard existed) counts as top-level.
        var ordered = sourceCodes
            .OrderBy(code => code.ParentCodeId is { } parentId && sourceById.ContainsKey(parentId) ? 1 : 0)
            .ThenBy(code => code.Code, StringComparer.Ordinal)
            .ToList();

        var copied = 0;
        var skippedExisting = 0;
        var skippedRetired = 0;

        foreach (var source in ordered)
        {
            if (!source.IsActive)
            {
                skippedRetired++;
                continue;
            }

            if (targetByCode.ContainsKey(source.Code))
            {
                skippedExisting++;
                continue;
            }

            var copy = source.CopyInto(command.PeriodId, TargetParentId(source, sourceById, targetByCode), command.ActorId);
            codes.Add(copy);
            targetByCode[copy.Code] = copy;
            copied++;
        }

        if (copied > 0)
        {
            await codes.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new BudgetCodeCopyResult(
            copied, skippedExisting, skippedRetired, sourceCodes.Count));
    }

    /// <summary>
    /// The target period's code the copy should roll up into: the one carrying the source parent's
    /// string, provided it is itself top-level. Null when the source code has no parent, the
    /// parent cannot be resolved, the target has no such code, or that code already has a parent
    /// of its own (the hierarchy is one level deep).
    /// </summary>
    private static Guid? TargetParentId(
        BudgetCode source,
        IReadOnlyDictionary<Guid, BudgetCode> sourceById,
        IReadOnlyDictionary<string, BudgetCode> targetByCode)
    {
        if (source.ParentCodeId is not { } parentId
            || !sourceById.TryGetValue(parentId, out var sourceParent)
            || !targetByCode.TryGetValue(sourceParent.Code, out var targetParent)
            || targetParent.ParentCodeId is not null)
        {
            return null;
        }

        return targetParent.Id;
    }
}
