using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Domain.BillableTrips;

namespace NorthernLink.Billing.Tests;

/// <summary>In-memory fake for consumer/handler tests — no EF, no Postgres.</summary>
public sealed class InMemoryBillableTripRepository : IBillableTripRepository
{
    public List<BillableTrip> Trips { get; } = [];

    public int SaveCount { get; private set; }

    public Task<bool> ExistsAsync(Guid tenantId, Guid tripId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Trips.Any(t => t.TenantId == tenantId && t.Id == tripId));

    public void Add(BillableTrip trip) => Trips.Add(trip);

    public Task<IReadOnlyList<BillableTrip>> GetUninvoicedForClientAsync(
        Guid clientId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BillableTrip>>(Trips
            .Where(t => t.ClientId == clientId
                && t.InvoiceId == null
                && t.ServiceDate >= periodStart
                && t.ServiceDate <= periodEnd)
            .ToList());

    public Task<IReadOnlyList<BillableTrip>> GetByIdsAsync(
        IReadOnlyCollection<Guid> tripIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BillableTrip>>(
            Trips.Where(t => tripIds.Contains(t.Id)).ToList());

    public Task<IReadOnlyList<BillableTrip>> GetClaimedByInvoiceAsync(
        Guid invoiceId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BillableTrip>>(
            Trips.Where(t => t.InvoiceId == invoiceId).ToList());

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
