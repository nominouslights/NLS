using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Application.Abstractions;

/// <summary>Write-side persistence for <see cref="ComplianceFinding"/> (tenant-scoped). Findings are never deleted — there is no remove.</summary>
public interface IComplianceFindingRepository
{
    Task<ComplianceFinding?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(ComplianceFinding finding);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
