namespace ShuttleApi.Domain.Billing;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invoice>> GetByClientIdAsync(Guid clientId, InvoiceStatus? status, CancellationToken cancellationToken = default);
    Task<int> GetCountForYearAsync(int year, CancellationToken cancellationToken = default);
    Task<HashSet<Guid>> GetInvoicedTripIdsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);
    Task UpdateAsync(Invoice invoice, CancellationToken cancellationToken = default);
}
