using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Actuals;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Actuals.Record;

/// <summary>
/// Handles <see cref="RecordActualTransactionCommand"/>. The order of the guards is the contract,
/// and it is <c>SetBudgetAllocationCommandHandler</c>'s order with the period question changed
/// from "may the plan change" to "does a period exist at all":
/// <list type="number">
/// <item><b>Validate the input</b> before any lookup — a malformed payload reports its validation
/// error, not a not-found for a code it was never going to reach.</item>
/// <item><b>A period covers the date.</b> Nothing is stored that the period-driven console could
/// never show; see <see cref="CoveringPeriodRule"/>.</item>
/// <item><b>The code exists in that period</b> (tenant-filtered, so another tenant's id reads as
/// NotFound; codes belong to a period, so another period's code does too) <b>and is active.</b> A retired code stays listed so old rows keep resolving, but it takes no
/// new spend — <see cref="ActualTransactionErrors.CodeRetired"/> names the way out.</item>
/// </list>
/// <b>What is deliberately absent is the period's state.</b> There is no
/// <c>AllowsPlanChanges</c> check anywhere in this handler, and that asymmetry with the
/// allocation handlers is the design, not an oversight: finalizing, reviewing and closing freeze
/// the <em>plan</em>. Actuals are facts — they keep arriving after a period ends, and a closed
/// period's actuals are exactly what the review is reading. See <see cref="ActualTransaction"/>.
/// </summary>
public sealed class RecordActualTransactionCommandHandler(
    IActualTransactionRepository transactions,
    IBudgetPeriodRepository periods,
    IBudgetCodeRepository codes)
    : ICommandHandler<RecordActualTransactionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        RecordActualTransactionCommand command,
        CancellationToken cancellationToken)
    {
        var validation = ActualTransaction.Validate(
            command.AmountCad, command.OccurredOn, command.Description);

        if (validation.IsFailure)
        {
            return Result.Failure<Guid>(validation.Error);
        }

        // Validate has already proved OccurredOn is present.
        var covered = await CoveringPeriodRule.ValidateAsync(
            periods, command.OccurredOn!.Value, cancellationToken);

        if (covered.IsFailure)
        {
            return Result.Failure<Guid>(covered.Error);
        }

        // Looked up in the covering period: codes belong to a period, and a transaction counts in
        // the one its date falls in. Another period's code reads as NotFound here.
        var code = await codes.GetByIdAsync(covered.Value.Id, command.BudgetCodeId, cancellationToken);
        if (code is null)
        {
            return Result.Failure<Guid>(BudgetCodeErrors.NotFound);
        }

        if (!code.IsActive)
        {
            return Result.Failure<Guid>(ActualTransactionErrors.CodeRetired);
        }

        var created = ActualTransaction.Create(
            command.TenantId,
            code.Id,
            code.Code,
            command.AmountCad,
            command.OccurredOn,
            command.Description,
            ActualTransactionSource.Manual,
            externalReference: null,
            command.ActorId);

        if (created.IsFailure)
        {
            return Result.Failure<Guid>(created.Error);
        }

        transactions.Add(created.Value);
        await transactions.SaveChangesAsync(cancellationToken);
        return Result.Success(created.Value.Id);
    }
}
