using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.WorkOrders;

namespace NorthernLink.Fleet.Tests;

/// <summary>In-memory fake of the work-order write-side repository for consumer tests.</summary>
internal sealed class InMemoryWorkOrderRepository : IWorkOrderRepository
{
    public List<WorkOrder> WorkOrders { get; } = [];

    public HashSet<Guid> KnownVehicleIds { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<WorkOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(WorkOrders.FirstOrDefault(w => w.Id == id));

    public Task<Guid?> FindVehicleIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<Guid?>(WorkOrders.FirstOrDefault(w => w.Id == id)?.VehicleId);

    public Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(KnownVehicleIds.Contains(vehicleId));

    /// <summary>Unit numbers for known vehicles; a vehicle with no entry has no unit to match.</summary>
    public Dictionary<Guid, string> VehicleUnitNumbers { get; } = [];

    public Task<string?> FindVehicleUnitNumberAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(VehicleUnitNumbers.TryGetValue(vehicleId, out var unit) ? unit : null);

    public void Add(WorkOrder workOrder) => WorkOrders.Add(workOrder);

    public Task<int> NextSequenceAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(WorkOrders.Count(w => w.TenantId == tenantId) + 1);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
