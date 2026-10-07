using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.BookeoImports;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// <see cref="IBookeoImportRepository"/> over <see cref="TripsDbContext"/> — request path only, so
/// every read goes through the tenant query filter (and RLS beneath it). Everything the import
/// loads is tracked and saved by the single <see cref="SaveAsync"/>, which is what makes a commit
/// (new trips, edited manifests, ledger, batch stamp) one transaction.
/// </summary>
internal sealed class BookeoImportRepository(TripsDbContext context) : IBookeoImportRepository
{
    public void AddBatch(BookeoImportBatch batch) => context.BookeoImportBatches.Add(batch);

    public Task<BookeoImportBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        context.BookeoImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

    public async Task<IReadOnlyList<BookeoImportBatch>> GetRecentBatchesAsync(int take, CancellationToken cancellationToken = default) =>
        await context.BookeoImportBatches
            .AsNoTracking()
            .OrderByDescending(b => b.UploadedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BookeoBooking>> GetBookingsAsync(
        IReadOnlyCollection<string> bookingNumbers, CancellationToken cancellationToken = default)
    {
        if (bookingNumbers.Count == 0)
        {
            return [];
        }

        var wanted = bookingNumbers.ToList();
        return await context.BookeoBookings
            .Where(b => wanted.Contains(b.BookingNumber))
            .ToListAsync(cancellationToken);
    }

    public void AddBooking(BookeoBooking booking) => context.BookeoBookings.Add(booking);

    public Task<int> CountBookingsOnTripAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        context.BookeoBookings.CountAsync(b => b.TripId == tripId, cancellationToken);

    public async Task<IReadOnlyList<BookeoProductMapping>> GetProductMappingsAsync(CancellationToken cancellationToken = default) =>
        await context.BookeoProductMappings.ToListAsync(cancellationToken);

    public void AddProductMapping(BookeoProductMapping mapping) => context.BookeoProductMappings.Add(mapping);

    public void RemoveProductMapping(BookeoProductMapping mapping) => context.BookeoProductMappings.Remove(mapping);

    public async Task<IReadOnlyList<BookeoUnitMapping>> GetUnitMappingsAsync(CancellationToken cancellationToken = default) =>
        await context.BookeoUnitMappings.ToListAsync(cancellationToken);

    public void AddUnitMapping(BookeoUnitMapping mapping) => context.BookeoUnitMappings.Add(mapping);

    public void RemoveUnitMapping(BookeoUnitMapping mapping) => context.BookeoUnitMappings.Remove(mapping);

    public async Task<IReadOnlyList<VehicleLookup>> GetVehiclesAsync(CancellationToken cancellationToken = default) =>
        await context.VehicleLookups.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Route>> GetRoutesAsync(
        IReadOnlyCollection<Guid> routeIds, CancellationToken cancellationToken = default)
    {
        if (routeIds.Count == 0)
        {
            return [];
        }

        var wanted = routeIds.ToList();
        return await context.Routes.AsNoTracking().Where(r => wanted.Contains(r.Id)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Trip>> GetTripsAsync(
        IReadOnlyCollection<DateOnly> serviceDates,
        IReadOnlyCollection<Guid> tripIds,
        CancellationToken cancellationToken = default)
    {
        if (serviceDates.Count == 0 && tripIds.Count == 0)
        {
            return [];
        }

        var dates = serviceDates.ToList();
        var ids = tripIds.ToList();
        return await context.Trips
            .Where(t => dates.Contains(t.ServiceDate) || ids.Contains(t.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, TripManifest>> GetManifestsForTripsAsync(
        IReadOnlyCollection<Trip> trips, CancellationToken cancellationToken = default)
    {
        if (trips.Count == 0)
        {
            return new Dictionary<Guid, TripManifest>();
        }

        var manifestIds = trips.Select(t => t.ManifestId).OfType<Guid>().ToList();
        var tripNumbers = trips.Where(t => t.ManifestId is null).Select(t => t.TripNumber).ToList();
        var manifests = await context.Manifests
            .Where(m => manifestIds.Contains(m.Id) || tripNumbers.Contains(m.TripNumber))
            .ToListAsync(cancellationToken);

        var byTrip = new Dictionary<Guid, TripManifest>();
        foreach (var trip in trips)
        {
            var manifest = trip.ManifestId is { } id
                ? manifests.FirstOrDefault(m => m.Id == id)
                : manifests.Where(m => m.TripNumber == trip.TripNumber).OrderBy(m => m.CreatedAtUtc).FirstOrDefault();
            if (manifest is not null)
            {
                byTrip[trip.Id] = manifest;
            }
        }

        return byTrip;
    }

    public void AddTrip(Trip trip) => context.Trips.Add(trip);

    public void AddManifest(TripManifest manifest) => context.Manifests.Add(manifest);

    public async Task<BookeoSaveOutcome> SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return BookeoSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var outcome = exception.Entries.Any(e => e.Entity is BookeoImportBatch)
                ? BookeoSaveOutcome.BatchAlreadyCommitted
                : BookeoSaveOutcome.Conflict;
            Discard();
            return outcome;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // A unique index rejected us: a ledger booking number or a mapping key that a
            // concurrent request committed first. The transaction rolled back; drop what the
            // tracker still holds so nothing half-written can be saved by accident later.
            Discard();
            return BookeoSaveOutcome.Conflict;
        }
    }

    private void Discard()
    {
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is Shared.Kernel.AggregateRoot aggregate)
            {
                aggregate.ClearDomainEvents();
            }

            entry.State = EntityState.Detached;
        }
    }
}
