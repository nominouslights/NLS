using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>
/// Write-side persistence for the PmCompletion aggregate (tenant-scoped). Append-only —
/// completions are the immutable per-unit service record, so there is no load or delete.
/// Reads go through the rm_pm_completions read model.
/// </summary>
public interface IPmCompletionRepository
{
    void Add(PmCompletion completion);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
