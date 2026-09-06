using Microsoft.EntityFrameworkCore;
using Npgsql;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Application.Integration;
using NorthernLink.Booking.Domain.BookingDays;
using NorthernLink.Booking.Domain.Customers;
using NorthernLink.Booking.Domain.Settings;

namespace NorthernLink.Booking.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="BookingDbContext"/> (tenant-filtered).</summary>
internal sealed class CustomerRepository(BookingDbContext context) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Add(Customer customer) => context.Customers.Add(customer);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}

/// <summary>Write-side repository for bookings — always loads the passenger children.</summary>
internal sealed class BookingRepository(BookingDbContext context) : IBookingRepository
{
    public Task<Domain.Bookings.Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Bookings
            .Include(b => b.Passengers)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public void Add(Domain.Bookings.Booking booking) => context.Bookings.Add(booking);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// BookingDay rows, incl. the DB-atomic get-or-create: insert optimistically, and when a
/// concurrent request won the (tenant_id, corridor_id, service_date) unique index race,
/// catch Postgres's 23505 and re-read the winner. Never an app-level existence pre-check —
/// the single-API-instance rule makes the DB the only safe arbiter.
/// </summary>
internal sealed class BookingDayRepository(BookingDbContext context) : IBookingDayRepository
{
    private const string UniqueViolation = "23505";

    public Task<BookingDay?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.BookingDays.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<BookingDay?> GetAsync(Guid corridorId, DateOnly serviceDate, CancellationToken cancellationToken = default) =>
        context.BookingDays.FirstOrDefaultAsync(
            d => d.CorridorId == corridorId && d.ServiceDate == serviceDate, cancellationToken);

    public async Task<IReadOnlyList<BookingDay>> GetForRangeAsync(
        Guid corridorId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        await context.BookingDays
            .AsNoTracking()
            .Where(d => d.CorridorId == corridorId && d.ServiceDate >= from && d.ServiceDate <= to)
            .OrderBy(d => d.ServiceDate)
            .ToListAsync(cancellationToken);

    public async Task<BookingDay> GetOrCreateAsync(
        Guid corridorId, DateOnly serviceDate, Func<BookingDay> factory, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(corridorId, serviceDate, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = factory();
        context.BookingDays.Add(created);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            // A concurrent request created the day between our read and our insert. Drop
            // our loser (and its audit rows, which rolled back with the transaction) and
            // return the row the winner committed.
            context.Entry(created).State = EntityState.Detached;
            created.ClearDomainEvents();

            return await GetAsync(corridorId, serviceDate, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"booking_days unique violation for corridor {corridorId} on {serviceDate:O}, " +
                    "but the winning row could not be re-read.");
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}

/// <summary>The tenant's policy singleton (the query filter scopes to the ambient tenant).</summary>
internal sealed class BookingPolicyRepository(BookingDbContext context) : IBookingPolicyRepository
{
    public Task<BookingPolicy?> GetAsync(CancellationToken cancellationToken = default) =>
        context.BookingPolicies.FirstOrDefaultAsync(cancellationToken);

    public void Add(BookingPolicy policy) => context.BookingPolicies.Add(policy);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}

/// <summary>Per-corridor override rows (tenant-filtered).</summary>
internal sealed class CorridorSettingsRepository(BookingDbContext context) : ICorridorSettingsRepository
{
    public Task<CorridorBookingSettings?> GetByCorridorAsync(
        Guid corridorId, CancellationToken cancellationToken = default) =>
        context.CorridorSettings.FirstOrDefaultAsync(s => s.CorridorId == corridorId, cancellationToken);

    public async Task<IReadOnlyList<CorridorBookingSettings>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        await context.CorridorSettings.AsNoTracking().ToListAsync(cancellationToken);

    public void Add(CorridorBookingSettings settings) => context.CorridorSettings.Add(settings);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Replica upserts over <see cref="BookingDbContext"/>. Reads go through the tenant query
/// filter (request path). The upsert runs from the integration handler, whose DbContext was
/// constructed before the handler pushed the event's tenant as the ambient tenant — so the
/// context's captured TenantId is null and the query filter would match nothing; the upsert
/// therefore bypasses the filter and matches on (key, tenant) explicitly. Postgres RLS
/// still scopes the statement to the event's tenant: the session variable is read at
/// connection open, inside the ambient push. Mirrors Trips' lookup repositories.
/// </summary>
internal sealed class CorridorLookupRepository(BookingDbContext context) : ICorridorLookupRepository
{
    public Task<CorridorLookup?> GetAsync(Guid corridorId, CancellationToken cancellationToken = default) =>
        context.CorridorLookups.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CorridorId == corridorId, cancellationToken);

    public async Task<IReadOnlyList<CorridorLookup>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.CorridorLookups.AsNoTracking().ToListAsync(cancellationToken);

    public async Task UpsertAsync(CorridorLookup corridor, CancellationToken cancellationToken = default)
    {
        var existing = await context.CorridorLookups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                c => c.CorridorId == corridor.CorridorId && c.TenantId == corridor.TenantId,
                cancellationToken);

        if (existing is null)
        {
            context.CorridorLookups.Add(corridor);
        }
        else
        {
            existing.Name = corridor.Name;
            existing.Origin = corridor.Origin;
            existing.Destination = corridor.Destination;
            existing.Active = corridor.Active;
            existing.UpdatedAtUtc = corridor.UpdatedAtUtc;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
