using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Services;

namespace NorthernLink.Fleet.Tests;

/// <summary>In-memory fake of the service-record write-side repository for consumer tests.</summary>
internal sealed class InMemoryServiceRecordRepository : IServiceRecordRepository
{
    public List<ServiceRecord> Records { get; } = [];

    public HashSet<Guid> KnownVehicleIds { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(KnownVehicleIds.Contains(vehicleId));

    public void Add(ServiceRecord record) => Records.Add(record);

    public Task<int> NextSequenceAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Records.Count(r => r.TenantId == tenantId) + 1);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
