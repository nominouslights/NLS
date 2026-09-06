using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Application.Bookings;
using NorthernLink.Booking.Application.Integration;
using NorthernLink.Booking.Domain.BookingDays;
using NorthernLink.Booking.Domain.Settings;
using BookingAggregate = NorthernLink.Booking.Domain.Bookings.Booking;

namespace NorthernLink.Booking.Tests;

/// <summary>In-memory fake of the policy repository for handler tests.</summary>
internal sealed class InMemoryBookingPolicyRepository : IBookingPolicyRepository
{
    public BookingPolicy? Policy { get; set; }

    public int SaveChangesCallCount { get; private set; }

    public Task<BookingPolicy?> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Policy);

    public void Add(BookingPolicy policy) => Policy = policy;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory fake of the corridor settings repository.</summary>
internal sealed class InMemoryCorridorSettingsRepository : ICorridorSettingsRepository
{
    public List<CorridorBookingSettings> Settings { get; } = [];

    public Task<CorridorBookingSettings?> GetByCorridorAsync(
        Guid corridorId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Settings.FirstOrDefault(s => s.CorridorId == corridorId));

    public Task<IReadOnlyList<CorridorBookingSettings>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CorridorBookingSettings>>(Settings);

    public void Add(CorridorBookingSettings settings) => Settings.Add(settings);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Deterministic clock for the 12-hour-window tests.</summary>
internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}

/// <summary>In-memory fake of the booking write repository.</summary>
internal sealed class InMemoryBookingRepository : IBookingRepository
{
    public List<BookingAggregate> Bookings { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<BookingAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Bookings.FirstOrDefault(b => b.Id == id));

    public void Add(BookingAggregate booking) => Bookings.Add(booking);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory fake of the booking-day repository (get-or-create included).</summary>
internal sealed class InMemoryBookingDayRepository : IBookingDayRepository
{
    public List<BookingDay> Days { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<BookingDay?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Days.FirstOrDefault(d => d.Id == id));

    public Task<BookingDay?> GetAsync(
        Guid corridorId, DateOnly serviceDate, CancellationToken cancellationToken = default) =>
        Task.FromResult(Days.FirstOrDefault(d => d.CorridorId == corridorId && d.ServiceDate == serviceDate));

    public Task<IReadOnlyList<BookingDay>> GetForRangeAsync(
        Guid corridorId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BookingDay>>(Days
            .Where(d => d.CorridorId == corridorId && d.ServiceDate >= from && d.ServiceDate <= to)
            .OrderBy(d => d.ServiceDate)
            .ToList());

    public async Task<BookingDay> GetOrCreateAsync(
        Guid corridorId, DateOnly serviceDate, Func<BookingDay> factory, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(corridorId, serviceDate, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = factory();
        Days.Add(created);
        return created;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory read side derived from live booking aggregates. NOTE: unlike the real
/// database-backed reads, rows reflect the aggregates' CURRENT (possibly unsaved) state —
/// the threshold service's overlay then just re-applies the same status, which keeps these
/// tests honest about the end result while the overlay logic itself is exercised by the
/// production query path.
/// </summary>
internal sealed class FakeBookingReadService : IBookingReadService
{
    public List<BookingAggregate> Bookings { get; } = [];

    /// <summary>Customer email per customer id (absent/null = customer has no email).</summary>
    public Dictionary<Guid, string?> EmailsByCustomerId { get; } = [];

    public Task<IReadOnlyList<BookingResponse>> GetForDateAsync(
        Guid corridorId, DateOnly serviceDate, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by the threshold tests.");

    public Task<IReadOnlyList<BookingSeatRow>> GetSeatRowsAsync(
        Guid corridorId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BookingSeatRow>>(Bookings
            .Where(b => b.CorridorId == corridorId && b.ServiceDate >= from && b.ServiceDate <= to)
            .Select(b => new BookingSeatRow(b.Id, b.ServiceDate, b.Status, b.HoldExpiresAtUtc, b.Passengers.Count))
            .ToList());

    public Task<IReadOnlyList<BookingRecipientRow>> GetRecipientRowsAsync(
        Guid corridorId, DateOnly serviceDate, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BookingRecipientRow>>(Bookings
            .Where(b => b.CorridorId == corridorId && b.ServiceDate == serviceDate)
            .Select(b => new BookingRecipientRow(
                b.Id,
                b.CustomerId,
                b.CustomerName,
                b.Status,
                EmailsByCustomerId.GetValueOrDefault(b.CustomerId)))
            .ToList());
}

/// <summary>In-memory fake of the corridor replica.</summary>
internal sealed class InMemoryCorridorLookupRepository : ICorridorLookupRepository
{
    public List<CorridorLookup> Corridors { get; } = [];

    public Task<CorridorLookup?> GetAsync(Guid corridorId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Corridors.FirstOrDefault(c => c.CorridorId == corridorId));

    public Task<IReadOnlyList<CorridorLookup>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CorridorLookup>>(Corridors);

    public Task UpsertAsync(CorridorLookup corridor, CancellationToken cancellationToken = default)
    {
        Corridors.RemoveAll(c => c.CorridorId == corridor.CorridorId && c.TenantId == corridor.TenantId);
        Corridors.Add(corridor);
        return Task.CompletedTask;
    }
}
