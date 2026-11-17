using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// In-memory fake of the inspection write-side repository for handler tests.
///
/// <see cref="TrySaveChangesAsync"/> can simulate the version race the duplicate cascade must
/// survive: while <see cref="ConflictsBeforeSave"/> is positive a save "loses" — exactly as the
/// EF repository does, nothing is persisted and the tracked state is dropped, which here means
/// every inspection loaded by the last <see cref="GetWithOpenDefectsAsync"/> gets its defects
/// back as they were when loaded (the rolled-back write never happened) and its unsaved events
/// discarded. <see cref="OnConflict"/> runs after the rollback, standing in for the OTHER writer
/// that won the race.
/// </summary>
internal sealed class InMemoryVehicleInspectionRepository : IVehicleInspectionRepository
{
    private readonly Dictionary<VehicleInspection, List<InspectionDefect>> _loaded = [];

    public List<VehicleInspection> Inspections { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    /// <summary>Successful <see cref="TrySaveChangesAsync"/> calls (the cascade's own save).</summary>
    public int TrySaveChangesCallCount { get; private set; }

    /// <summary>Every <see cref="TrySaveChangesAsync"/> call, won or lost.</summary>
    public int TrySaveAttemptCount { get; private set; }

    public int GetWithOpenDefectsCallCount { get; private set; }

    public int DiscardChangesCallCount { get; private set; }

    /// <summary>How many of the next <see cref="TrySaveChangesAsync"/> calls lose the version race.</summary>
    public int ConflictsBeforeSave { get; set; }

    /// <summary>The concurrent writer: runs after each lost race, once the rollback is done.</summary>
    public Action<int>? OnConflict { get; set; }

    /// <summary>When set, the next <see cref="TrySaveChangesAsync"/> throws it (a non-concurrency failure).</summary>
    public Exception? ThrowOnTrySave { get; set; }

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

    public Task<VehicleInspection?> GetByGeneratedWorkOrderIdAsync(
        Guid workOrderId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Inspections.FirstOrDefault(i => i.GeneratedWorkOrderId == workOrderId));

    /// <summary>Mirrors the SQL: the vehicle scope plus "has an unresolved defect" — no item predicate.</summary>
    public Task<IReadOnlyList<VehicleInspection>> GetWithOpenDefectsAsync(
        DefectVehicleScope scope,
        CancellationToken cancellationToken = default)
    {
        GetWithOpenDefectsCallCount++;
        IReadOnlyList<VehicleInspection> matches = Inspections
            .Where(scope.Includes)
            .Where(i => i.Defects.Any(d => !d.IsResolved))
            .ToList();

        _loaded.Clear();
        foreach (var inspection in matches)
        {
            _loaded[inspection] = [.. inspection.Defects];
        }

        return Task.FromResult(matches);
    }

    public void Add(VehicleInspection inspection) => Inspections.Add(inspection);

    public void Remove(VehicleInspection inspection) => Inspections.Remove(inspection);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }

    public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TrySaveAttemptCount++;

        if (ThrowOnTrySave is { } failure)
        {
            ThrowOnTrySave = null;
            throw failure;
        }

        if (ConflictsBeforeSave > 0)
        {
            ConflictsBeforeSave--;
            RollBackLoaded();
            OnConflict?.Invoke(TrySaveAttemptCount);
            return Task.FromResult(false);
        }

        _loaded.Clear();
        TrySaveChangesCallCount++;
        return Task.FromResult(true);
    }

    public void DiscardChanges()
    {
        DiscardChangesCallCount++;
        RollBackLoaded();
    }

    private void RollBackLoaded()
    {
        foreach (var (inspection, defects) in _loaded)
        {
            // Defects has a private setter on the aggregate; the fake restores the persisted
            // state the way a re-read after ChangeTracker.Clear() would see it.
            typeof(VehicleInspection).GetProperty(nameof(VehicleInspection.Defects))!.SetValue(inspection, defects);
            inspection.ClearDomainEvents();
        }

        _loaded.Clear();
    }
}
