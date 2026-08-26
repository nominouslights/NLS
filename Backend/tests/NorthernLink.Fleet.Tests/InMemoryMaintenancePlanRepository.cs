using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Tests;

/// <summary>In-memory fake of the maintenance-plan write-side repository for handler tests.</summary>
internal sealed class InMemoryMaintenancePlanRepository : IMaintenancePlanRepository
{
    public List<MaintenancePlan> Plans { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    /// <summary>
    /// Simulates losing the check-then-insert race: when set, the next <see cref="TryAddAsync"/>
    /// reports a unique-name conflict and this plan appears in <see cref="Plans"/> as the
    /// concurrent winner's row (so a re-probe by name finds it).
    /// </summary>
    public MaintenancePlan? ConcurrentWinnerOnNextTryAdd { get; set; }

    public Task<MaintenancePlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Plans.FirstOrDefault(p => p.Id == id));

    public Task<MaintenancePlan?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Plans.Any(p => p.Id == id));

    public Task<Guid?> FindIdByNameAsync(string name, CancellationToken cancellationToken = default) =>
        // Dumb equality, like the real repository — trim ownership sits with the caller.
        Task.FromResult<Guid?>(Plans.FirstOrDefault(p => p.Name == name)?.Id);

    public void Add(MaintenancePlan plan) => Plans.Add(plan);

    public Task<bool> TryAddAsync(MaintenancePlan plan, CancellationToken cancellationToken = default)
    {
        if (ConcurrentWinnerOnNextTryAdd is { } winner)
        {
            ConcurrentWinnerOnNextTryAdd = null;
            Plans.Add(winner);
            return Task.FromResult(false);
        }

        if (Plans.Any(p => p.TenantId == plan.TenantId && p.Name == plan.Name))
        {
            return Task.FromResult(false);
        }

        Plans.Add(plan);
        SaveChangesCallCount++;
        return Task.FromResult(true);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
