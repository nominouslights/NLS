using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.PurchaseOrders;

namespace NorthernLink.Clients.Application.PurchaseOrders.Update;

public sealed class UpdatePurchaseOrderCommandHandler(IPurchaseOrderRepository repository)
    : ICommandHandler<UpdatePurchaseOrderCommand>
{
    public async Task<Result> Handle(UpdatePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        var purchaseOrder = await repository.GetByIdAsync(command.PurchaseOrderId, cancellationToken);
        if (purchaseOrder is null)
        {
            return Result.Failure(PurchaseOrderErrors.NotFound);
        }

        var result = purchaseOrder.Update(
            command.PoNumber,
            command.Issued,
            command.Expiry,
            command.AmountCad,
            command.Note);

        if (result.IsFailure)
        {
            return result;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
