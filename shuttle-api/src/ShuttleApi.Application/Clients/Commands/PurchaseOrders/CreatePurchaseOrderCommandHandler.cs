using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Application.Clients;

internal sealed class CreatePurchaseOrderCommandHandler(
    IClientRepository clientRepository,
    IContractRepository contractRepository,
    IPurchaseOrderRepository purchaseOrderRepository)
    : IRequestHandler<CreatePurchaseOrderCommand, CreatePurchaseOrderResult>
{
    public async Task<CreatePurchaseOrderResult> Handle(
        CreatePurchaseOrderCommand request,
        CancellationToken cancellationToken)
    {
        _ = await clientRepository.GetByIdAsync(request.ClientId, cancellationToken)
            ?? throw new NotFoundException($"Client {request.ClientId} not found.");

        if (await purchaseOrderRepository.ExistsByClientAndPoNumberAsync(
                request.ClientId,
                request.PoNumber,
                cancellationToken: cancellationToken))
        {
            throw new ConflictException(
                $"A purchase order with number '{request.PoNumber.Trim()}' already exists for this client.");
        }

        var contractIds = request.ContractIds ?? [];
        await ValidateContractLinksAsync(request.ClientId, contractIds, cancellationToken);

        var lineItems = request.LineItems
            .Select(i => (i.Description, i.UnitRate, i.Quantity))
            .ToList();

        var purchaseOrder = PurchaseOrder.Create(
            request.ClientId,
            request.PoNumber,
            DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
            request.Details,
            lineItems);

        await purchaseOrderRepository.AddAsync(purchaseOrder, contractIds, cancellationToken);

        return new CreatePurchaseOrderResult(purchaseOrder.Id);
    }

    private async Task ValidateContractLinksAsync(
        Guid clientId,
        IReadOnlyList<Guid> contractIds,
        CancellationToken cancellationToken)
    {
        foreach (var contractId in contractIds.Distinct())
        {
            var contract = await contractRepository.GetByIdAsync(contractId, cancellationToken)
                ?? throw new NotFoundException($"Contract {contractId} not found.");

            if (contract.ClientId != clientId)
                throw new ArgumentException($"Contract {contractId} does not belong to client {clientId}.");
        }
    }
}
