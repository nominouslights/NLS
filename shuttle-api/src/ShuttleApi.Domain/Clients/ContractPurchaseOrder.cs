namespace ShuttleApi.Domain.Clients;

public sealed class ContractPurchaseOrder
{
    public Guid ContractId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }

    private ContractPurchaseOrder() { }

    public static ContractPurchaseOrder Create(Guid contractId, Guid purchaseOrderId) =>
        new() { ContractId = contractId, PurchaseOrderId = purchaseOrderId };
}
