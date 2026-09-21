using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="TripsDbContext"/> (tenant-filtered).</summary>
internal sealed class TripRepository(TripsDbContext context) : ITripRepository
{
    public void Add(Trip trip) => context.Trips.Add(trip);

    public Task<Trip?> GetByIdAsync(Guid tripId, CancellationToken cancellationToken = default) =>
        context.Trips.FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);

    public Task<Trip?> GetByTripNumberAsync(string tripNumber, CancellationToken cancellationToken = default) =>
        context.Trips.FirstOrDefaultAsync(t => t.TripNumber == tripNumber, cancellationToken);

    public async Task<IReadOnlyList<Trip>> GetByRoundTripKeyAsync(
        string roundTripKey, CancellationToken cancellationToken = default) =>
        await context.Trips
            .Where(t => t.RoundTripKey == roundTripKey)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Trip>> GetByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> tripIds,
        CancellationToken cancellationToken = default)
    {
        if (tripIds.Count == 0)
        {
            return [];
        }

        return await context.Trips
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && tripIds.Contains(t.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<Trip?> GetByBookingDayIdAsync(
        Guid tenantId,
        Guid bookingDayId,
        CancellationToken cancellationToken = default) =>
        context.Trips
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                t => t.TenantId == tenantId && t.BookingDayId == bookingDayId, cancellationToken);

    public async Task<bool> TryAddForBookingDayAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        context.Trips.Add(trip);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // The (tenant_id, booking_day_id) unique index rejected us — another processing
            // of the same confirmation won. Drop every pending insert (the trip AND the
            // audit/outbox rows the save pipeline staged for it — they rolled back with the
            // transaction but are still tracked as Added) and report the duplicate.
            foreach (var entry in context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added)
                .ToList())
            {
                entry.State = EntityState.Detached;
            }

            trip.ClearDomainEvents();
            return false;
        }
    }

    public async Task<IReadOnlySet<(DateOnly ServiceDate, TripDirection Direction)>> GetGeneratedOccurrenceKeysAsync(
        Guid templateId,
        DateOnly from,
        DateOnly toExclusive,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.Trips
            .AsNoTracking()
            .Where(t => t.ScheduleTemplateId == templateId
                && t.ServiceDate >= from
                && t.ServiceDate < toExclusive
                && t.Direction != null)
            .Select(t => new { t.ServiceDate, t.Direction })
            .ToListAsync(cancellationToken);

        return existing
            .Select(t => (t.ServiceDate, t.Direction!.Value))
            .ToHashSet();
    }

    public async Task<bool> TryAddGeneratedAsync(IReadOnlyList<Trip> trips, CancellationToken cancellationToken = default)
    {
        foreach (var trip in trips)
        {
            context.Trips.Add(trip);
        }

        try
        {
            // One transaction per template: every new leg (plus its journal/snapshot/outbox
            // rows) commits together, or nothing does.
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // The (tenant, template, service date, direction) unique index rejected one of
            // the legs — a concurrent run got there first. Same cleanup as the booking-day
            // path: detach every pending insert and drop the unsaved events.
            foreach (var entry in context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added)
                .ToList())
            {
                entry.State = EntityState.Detached;
            }

            foreach (var trip in trips)
            {
                trip.ClearDomainEvents();
            }

            return false;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
