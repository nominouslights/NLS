using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.PurchaseOrders;

namespace NorthernLink.Clients.Application.PurchaseOrders.Delete;

public sealed class DeletePurchaseOrderCommandHandler(IPurchaseOrderRepository repository)
    : ICommandHandler<DeletePurchaseOrderCommand>
{
    public async Task<Result> Handle(DeletePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        var purchaseOrder = await repository.GetByIdAsync(command.PurchaseOrderId, cancellationToken);
        if (purchaseOrder is null)
        {
            return Result.Failure(PurchaseOrderErrors.NotFound);
        }

        // Raise before removing: a hard delete raises nothing on its own, and Billing's
        // purchase_order_snapshots replica has to be told to drop the row or it keeps pricing
        // work against withdrawn terms. (The Fleet inspection-removal precedent.)
        purchaseOrder.MarkDeleted();
        repository.Remove(purchaseOrder);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
