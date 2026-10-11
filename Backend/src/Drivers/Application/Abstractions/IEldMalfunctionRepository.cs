using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Application.Abstractions;

/// <summary>Write-side persistence for <see cref="EldMalfunctionReport"/> (tenant-scoped).</summary>
public interface IEldMalfunctionRepository
{
    Task<EldMalfunctionReport?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(EldMalfunctionReport report);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
