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

    public Task<TripManifest?> GetForTripAsync(
        Guid? manifestId,
        string tripNumber,
        CancellationToken cancellationToken = default) =>
        manifestId is { } id
            ? context.Manifests.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            : context.Manifests
                .Where(m => m.TripNumber == tripNumber)
                .OrderBy(m => m.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TripManifest>> GetByTripNumberAsync(
        string tripNumber,
        CancellationToken cancellationToken = default) =>
        await context.Manifests
            .Where(m => m.TripNumber == tripNumber)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public void Remove(TripManifest manifest) => context.Manifests.Remove(manifest);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
