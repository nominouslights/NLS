using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Domain.Clients;
using NorthernLink.Clients.Domain.PurchaseOrders;

namespace NorthernLink.Clients.Application.PurchaseOrders.Create;

public sealed class CreatePurchaseOrderCommandHandler(
    IClientRepository clientRepository,
    IPurchaseOrderRepository purchaseOrderRepository)
    : ICommandHandler<CreatePurchaseOrderCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreatePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        if (!await clientRepository.ExistsAsync(command.ClientId, cancellationToken))
        {
            return Result.Failure<Guid>(ClientErrors.NotFound);
        }

        var purchaseOrderResult = PurchaseOrder.Create(
            command.TenantId,
            command.ClientId,
            command.PoNumber,
            command.Issued,
            command.Expiry,
            command.AmountCad,
            command.Note);

        if (purchaseOrderResult.IsFailure)
        {
            return Result.Failure<Guid>(purchaseOrderResult.Error);
        }

        purchaseOrderRepository.Add(purchaseOrderResult.Value);
        await purchaseOrderRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(purchaseOrderResult.Value.Id);
    }
}
