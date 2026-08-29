using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>
/// Write-side persistence for the PmCompletion aggregate (tenant-scoped). Append-only —
/// completions are the immutable per-unit service record, so there is no update or delete.
/// Queries go through the rm_pm_completions read model; <see cref="GetByIdAsync"/> exists
/// only for same-module reactions (odometer propagation) that need the just-logged row.
/// </summary>
public interface IPmCompletionRepository
{
    Task<PmCompletion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(PmCompletion completion);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
