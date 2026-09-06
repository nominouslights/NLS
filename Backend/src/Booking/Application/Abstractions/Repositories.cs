using NorthernLink.Booking.Application.Integration;
using NorthernLink.Booking.Domain.BookingDays;
using NorthernLink.Booking.Domain.Bookings;
using NorthernLink.Booking.Domain.Customers;
using NorthernLink.Booking.Domain.Settings;

namespace NorthernLink.Booking.Application.Abstractions;

/// <summary>Write-side repository for the Customer aggregate (tenant-filtered).</summary>
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Customer customer);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Write-side repository for the Booking aggregate (tenant-filtered, passengers loaded).</summary>
public interface IBookingRepository
{
    Task<Domain.Bookings.Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Domain.Bookings.Booking booking);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for BookingDay rows (tenant-filtered). Get-or-create MUST be DB-atomic:
/// the implementation inserts optimistically and, on the (tenant_id, corridor_id,
/// service_date) unique violation, re-reads the row a concurrent request created —
/// never an application-level existence check (single-instance rule).
/// </summary>
public interface IBookingDayRepository
{
    Task<BookingDay?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BookingDay?> GetAsync(Guid corridorId, DateOnly serviceDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookingDay>> GetForRangeAsync(
        Guid corridorId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>Returns the existing day, or creates and SAVES a new one (unique-violation safe).</summary>
    Task<BookingDay> GetOrCreateAsync(
        Guid corridorId, DateOnly serviceDate, Func<BookingDay> factory, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Repository for the tenant's policy singleton (tenant-filtered; null until first write).</summary>
public interface IBookingPolicyRepository
{
    Task<BookingPolicy?> GetAsync(CancellationToken cancellationToken = default);

    void Add(BookingPolicy policy);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Repository for per-corridor settings rows (tenant-filtered).</summary>
public interface ICorridorSettingsRepository
{
    Task<CorridorBookingSettings?> GetByCorridorAsync(Guid corridorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CorridorBookingSettings>> GetAllAsync(CancellationToken cancellationToken = default);

    void Add(CorridorBookingSettings settings);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Replica upserts + reads for booking.corridor_lookup (see CorridorLookup).</summary>
public interface ICorridorLookupRepository
{
    Task<CorridorLookup?> GetAsync(Guid corridorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CorridorLookup>> GetAllAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(CorridorLookup corridor, CancellationToken cancellationToken = default);
}
