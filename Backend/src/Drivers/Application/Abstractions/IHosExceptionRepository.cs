using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Application.Abstractions;

/// <summary>Write-side persistence for <see cref="HosException"/> (tenant-scoped: EF query filter + Postgres RLS). Reads go through <see cref="IComplianceRecordsReader"/>.</summary>
public interface IHosExceptionRepository
{
    Task<HosException?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(HosException exception);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
