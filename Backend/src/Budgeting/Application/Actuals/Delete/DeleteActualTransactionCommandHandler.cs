using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Domain.Actuals;

namespace NorthernLink.Budgeting.Application.Actuals.Delete;

/// <summary>
/// Handles <see cref="DeleteActualTransactionCommand"/>. One guard: the row must exist, and
/// another tenant's id reads as NotFound through the tenant query filter.
/// <para>
/// Nothing else is checked, and the two absences are both deliberate. There is no period-state
/// guard — actuals are not frozen by the lifecycle, so a duplicate entered against a closed
/// period can still be taken out, which is precisely when someone reviewing the books would
/// notice it. And there is no retired-code guard — clearing a row off a retired code is exactly
/// the clean-up that needs to be possible, the same reasoning as
/// <c>RemoveBudgetAllocationCommandHandler</c>.
/// </para>
/// <para>
/// The aggregate needs no <c>Delete()</c> method and raises no event: <c>AppendAuditEntries</c>
/// exempts deletes and writes a final snapshot plus the synthetic <c>aggregate-deleted</c>
/// journal row, which is what drives the projection to drop the read row.
/// </para>
/// </summary>
public sealed class DeleteActualTransactionCommandHandler(IActualTransactionRepository transactions)
    : ICommandHandler<DeleteActualTransactionCommand>
{
    public async Task<Result> Handle(DeleteActualTransactionCommand command, CancellationToken cancellationToken)
    {
        var transaction = await transactions.GetByIdAsync(command.TransactionId, cancellationToken);
        if (transaction is null)
        {
            return Result.Failure(ActualTransactionErrors.NotFound);
        }

        transactions.Remove(transaction);
        await transactions.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
