using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Tests;

/// <summary>In-memory fake of the inspection write-side repository for consumer tests.</summary>
internal sealed class InMemoryVehicleInspectionRepository : IVehicleInspectionRepository
{
    public List<VehicleInspection> Inspections { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<bool> ExistsForManifestAsync(
        Guid tenantId,
        Guid manifestId,
        InspectionType type,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Inspections.Any(i =>
            i.TenantId == tenantId && i.ManifestId == manifestId && i.Type == type));

    public Task<bool> ExistsForTripAsync(
        Guid tenantId,
        string tripNumber,
        InspectionType type,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Inspections.Any(i =>
            i.TenantId == tenantId && i.TripNumber == tripNumber && i.Type == type));

    public Task<VehicleInspection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Inspections.FirstOrDefault(i => i.Id == id));

    public void Add(VehicleInspection inspection) => Inspections.Add(inspection);

    public void Remove(VehicleInspection inspection) => Inspections.Remove(inspection);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
