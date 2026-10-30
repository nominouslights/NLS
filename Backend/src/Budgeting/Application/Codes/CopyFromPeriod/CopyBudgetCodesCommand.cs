using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Codes.CopyFromPeriod;

/// <summary>
/// Seeds <paramref name="PeriodId"/>'s chart from another period's: every <b>active</b> code of
/// <paramref name="SourcePeriodId"/> whose string the target does not already have is re-created
/// in the target as an active code with a new id and every descriptive field carried across,
/// hierarchy included (see <c>BudgetCode.CopyInto</c> and the handler).
/// <para>
/// <paramref name="SourcePeriodId"/> is nullable so an omitted field fails as a readable
/// <c>Budgeting.Code.CopySourceRequired</c> rather than binding to <see cref="Guid.Empty"/> — the
/// <c>CopyBudgetAllocationsCommand</c> reasoning. <paramref name="ActorId"/> comes from the signed
/// token and lands as <c>created_by</c> on every copied code.
/// </para>
/// <para>
/// Nothing is ever overwritten and nothing is removed: running it twice is safe — the second run
/// copies nothing.
/// </para>
/// </summary>
public sealed record CopyBudgetCodesCommand(
    Guid TenantId,
    Guid PeriodId,
    Guid? SourcePeriodId,
    Guid? ActorId) : ICommand<BudgetCodeCopyResult>;
