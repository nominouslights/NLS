using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Tests;

/// <summary>In-memory fake of the maintenance-plan write-side repository for handler tests.</summary>
internal sealed class InMemoryMaintenancePlanRepository : IMaintenancePlanRepository
{
    public List<MaintenancePlan> Plans { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<MaintenancePlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Plans.FirstOrDefault(p => p.Id == id));

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Plans.Any(p => p.Id == id));

    public Task<bool> ExistsByNameAsync(
        string name, Guid? excludePlanId = null, CancellationToken cancellationToken = default) =>
        // Dumb equality, like the real repository — trim ownership sits with the caller.
        Task.FromResult(Plans.Any(p => p.Name == name && (excludePlanId is null || p.Id != excludePlanId)));

    public void Add(MaintenancePlan plan) => Plans.Add(plan);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
