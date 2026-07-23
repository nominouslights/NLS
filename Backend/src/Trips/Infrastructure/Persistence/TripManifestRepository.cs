using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="TripsDbContext"/> (tenant-filtered).</summary>
internal sealed class TripManifestRepository(TripsDbContext context) : ITripManifestRepository
{
    public void Add(TripManifest manifest) => context.Manifests.Add(manifest);

    public Task<TripManifest?> GetByIdAsync(Guid manifestId, CancellationToken cancellationToken = default) =>
        context.Manifests.FirstOrDefaultAsync(m => m.Id == manifestId, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
