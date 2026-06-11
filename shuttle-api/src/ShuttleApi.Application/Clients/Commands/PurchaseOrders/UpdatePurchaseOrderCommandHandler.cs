using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Application.Clients;

internal sealed class UpdatePurchaseOrderCommandHandler(
    IContractRepository contractRepository,
    IPurchaseOrderRepository purchaseOrderRepository)
    : IRequestHandler<UpdatePurchaseOrderCommand>
{
    public async Task Handle(UpdatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var purchaseOrder = await purchaseOrderRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Purchase order {request.Id} not found.");

        if (purchaseOrder.ClientId != request.ClientId)
            throw new ArgumentException("Purchase order does not belong to the specified client.");

        if (await purchaseOrderRepository.ExistsByClientAndPoNumberAsync(
                request.ClientId,
                request.PoNumber,
                request.Id,
                cancellationToken))
        {
            throw new ConflictException(
                $"A purchase order with number '{request.PoNumber.Trim()}' already exists for this client.");
        }

        var contractIds = request.ContractIds ?? [];
        foreach (var contractId in contractIds.Distinct())
        {
            var contract = await contractRepository.GetByIdAsync(contractId, cancellationToken)
                ?? throw new NotFoundException($"Contract {contractId} not found.");

            if (contract.ClientId != request.ClientId)
                throw new ArgumentException($"Contract {contractId} does not belong to client {request.ClientId}.");
        }

        var lineItems = request.LineItems
            .Select(i => (i.Description, i.UnitRate, i.Quantity))
            .ToList();

        purchaseOrder.Update(
            request.PoNumber,
            DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
            request.Details,
            lineItems);

        await purchaseOrderRepository.UpdateAsync(purchaseOrder, contractIds, cancellationToken);
    }
}
