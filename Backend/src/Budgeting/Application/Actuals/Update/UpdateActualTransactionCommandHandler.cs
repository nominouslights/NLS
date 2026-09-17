using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Actuals;
using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Actuals.Update;

/// <summary>
/// Handles <see cref="UpdateActualTransactionCommand"/>. Same guards as recording, plus the row
/// itself:
/// <list type="number">
/// <item><b>Validate the input</b> before any lookup.</item>
/// <item><b>The transaction exists</b> (tenant-filtered, so another tenant's id reads as
/// NotFound) — it is what the request is addressed to.</item>
/// <item><b>A period covers the new date.</b> Moving a transaction's date is how it moves between
/// periods, so a correction can strand a row exactly as a bad entry can.</item>
/// <item><b>The code exists and is active.</b> A correction onto a retired code is a new decision
/// against a code no longer offered, so it is refused the same way a first entry is — including
/// when the row is already on that code and only its description is being fixed. Restoring the
/// code or picking another is the way through, and either leaves a clearer record than a
/// correction quietly landing somewhere nobody offers.</item>
/// </list>
/// As on the record path, the period's <em>state</em> is deliberately never consulted: a closed
/// period's actuals must stay correctable, because that is when anyone is looking closely enough
/// to spot an error. Only the plan is frozen by the lifecycle.
/// </summary>
public sealed class UpdateActualTransactionCommandHandler(
    IActualTransactionRepository transactions,
    IBudgetPeriodRepository periods,
    IBudgetCodeRepository codes)
    : ICommandHandler<UpdateActualTransactionCommand>
{
    public async Task<Result> Handle(UpdateActualTransactionCommand command, CancellationToken cancellationToken)
    {
        var validation = ActualTransaction.Validate(
            command.AmountCad, command.OccurredOn, command.Description);

        if (validation.IsFailure)
        {
            return validation;
        }

        var transaction = await transactions.GetByIdAsync(command.TransactionId, cancellationToken);
        if (transaction is null)
        {
            return Result.Failure(ActualTransactionErrors.NotFound);
        }

        // Validate has already proved OccurredOn is present.
        var covered = await CoveringPeriodRule.ValidateAsync(
            periods, command.OccurredOn!.Value, cancellationToken);

        if (covered.IsFailure)
        {
            return covered;
        }

        var code = await codes.GetByIdAsync(command.BudgetCodeId, cancellationToken);
        if (code is null)
        {
            return Result.Failure(BudgetCodeErrors.NotFound);
        }

        if (!code.IsActive)
        {
            return Result.Failure(ActualTransactionErrors.CodeRetired);
        }

        var update = transaction.Update(
            code.Id,
            code.Code,
            command.AmountCad,
            command.OccurredOn,
            command.Description,
            command.ActorId);

        if (update.IsFailure)
        {
            return update;
        }

        await transactions.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
