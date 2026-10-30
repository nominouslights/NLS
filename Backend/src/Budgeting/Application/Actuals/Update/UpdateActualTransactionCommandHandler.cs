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
/// <item><b>The code exists, and is active <em>if the edit moves the transaction onto it</em>.</b>
/// See below — this is narrower than the record path's check, deliberately.</item>
/// </list>
/// <para>
/// <b>A retired code blocks a move onto it, never an edit that leaves it alone.</b> Putting new
/// spend on a retired code is a new decision against a code nobody offers any more, so that is
/// refused exactly as a first entry is. But a transaction already sitting on a code that has
/// since been retired must stay correctable: a retired code must never stand between anyone and
/// fixing a fact. Without the narrowing, a typo in a description on historical data would be
/// unfixable without restoring a code the tenant has deliberately taken out of use.
/// </para>
/// <para>
/// <c>BudgetCodeRetirementRule</c> makes that situation rarer — a code cannot be retired while a
/// period using it is still open — but not impossible, because actuals stay editable in a Closed
/// period by design, and a Closed period is precisely where a code is free to be retired.
/// </para>
/// <para>
/// As on the record path, the period's <em>state</em> is deliberately never consulted: a closed
/// period's actuals must stay correctable, because that is when anyone is looking closely enough
/// to spot an error. Only the plan is frozen by the lifecycle.
/// </para>
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

        // Looked up in the covering period: codes belong to a period, and a transaction counts in
        // the one its date falls in. Another period's code reads as NotFound here.
        var code = await codes.GetByIdAsync(covered.Value.Id, command.BudgetCodeId, cancellationToken);
        if (code is null)
        {
            return Result.Failure(BudgetCodeErrors.NotFound);
        }

        // Only a move onto a retired code is refused. Compared against the row's current code, not
        // against the request's own field, so "did this edit change anything" is answered by the
        // stored state rather than by what the caller chose to send.
        var movesToAnotherCode = code.Id != transaction.BudgetCodeId;
        if (movesToAnotherCode && !code.IsActive)
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
