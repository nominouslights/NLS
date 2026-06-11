namespace ShuttleApi.Domain.Clients;
public interface IPurchaseOrderRepository
{
    Task<IReadOnlyList<PurchaseOrder>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByClientAndPoNumberAsync(
        Guid clientId,
        string poNumber,
        Guid? excludePurchaseOrderId = null,
        CancellationToken cancellationToken = default);
    Task AddAsync(PurchaseOrder purchaseOrder, IReadOnlyList<Guid> contractIds, CancellationToken cancellationToken = default);
    Task UpdateAsync(PurchaseOrder purchaseOrder, IReadOnlyList<Guid> contractIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetLinkedContractIdsAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
}
