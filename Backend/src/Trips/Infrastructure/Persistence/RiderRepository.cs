using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Riders;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="TripsDbContext"/> (tenant-filtered).</summary>
internal sealed class RiderRepository(TripsDbContext context) : IRiderRepository
{
    public void Add(Rider rider) => context.Riders.Add(rider);

    public Task<Rider?> GetByIdAsync(Guid riderId, CancellationToken cancellationToken = default) =>
        context.Riders.FirstOrDefaultAsync(r => r.Id == riderId, cancellationToken);

    public Task<Rider?> GetByKeyAsync(
        TripServiceType serviceType,
        string normalizedName,
        CancellationToken cancellationToken = default) =>
        context.Riders.FirstOrDefaultAsync(
            r => r.ServiceType == serviceType && r.NormalizedName == normalizedName,
            cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
