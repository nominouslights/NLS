using Microsoft.EntityFrameworkCore;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Application.Bookings;
using NorthernLink.Booking.Application.Customers;
using NorthernLink.Booking.Domain.Bookings;
using NorthernLink.Booking.Domain.Customers;

namespace NorthernLink.Booking.Infrastructure.Persistence;

/// <summary>
/// Read side — queries the aggregate tables directly (Booking keeps no rm_* projections:
/// seat math is derived per read, and the write shapes already serve the read side).
/// Search matches a name substring case-insensitively OR the digits-only phone column
/// against the digits of the search term, so any phone formatting finds the customer.
/// </summary>
internal sealed class CustomerReadService(BookingDbContext context) : ICustomerReadService
{
    public async Task<IReadOnlyList<CustomerResponse>> SearchAsync(
        string? search, CancellationToken cancellationToken = default)
    {
        var query = context.Customers.AsNoTracking();

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var digits = Customer.NormalizePhone(term);
            var namePattern = $"%{EscapeLike(term)}%";
            query = digits is null
                ? query.Where(c => EF.Functions.ILike(c.Name, namePattern))
                : query.Where(c =>
                    EF.Functions.ILike(c.Name, namePattern)
                    || (c.PhoneDigits != null && c.PhoneDigits.Contains(digits)));
        }

        var customers = await query
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return customers.Select(ToResponse).ToList();
    }

    public async Task<CustomerResponse?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        return customer is null ? null : ToResponse(customer);
    }

    private static CustomerResponse ToResponse(Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.Phone,
        customer.Email,
        customer.Notes,
        customer.CreatedAtUtc,
        customer.UpdatedAtUtc);

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}

/// <summary>Read side for bookings — the day panel's full list and the month's seat rows.</summary>
internal sealed class BookingReadService(BookingDbContext context) : IBookingReadService
{
    public async Task<IReadOnlyList<BookingResponse>> GetForDateAsync(
        Guid corridorId, DateOnly serviceDate, CancellationToken cancellationToken = default)
    {
        var bookings = await context.Bookings
            .AsNoTracking()
            .Include(b => b.Passengers)
            .Where(b => b.CorridorId == corridorId && b.ServiceDate == serviceDate)
            .OrderBy(b => b.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        return bookings.Select(b => ToResponse(b, now)).ToList();
    }

    public async Task<IReadOnlyList<BookingSeatRow>> GetSeatRowsAsync(
        Guid corridorId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        await context.Bookings
            .AsNoTracking()
            .Where(b => b.CorridorId == corridorId && b.ServiceDate >= from && b.ServiceDate <= to)
            .Select(b => new BookingSeatRow(
                b.Id,
                b.ServiceDate,
                b.Status,
                b.HoldExpiresAtUtc,
                b.Passengers.Count))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BookingRecipientRow>> GetRecipientRowsAsync(
        Guid corridorId, DateOnly serviceDate, CancellationToken cancellationToken = default) =>
        await context.Bookings
            .AsNoTracking()
            .Where(b => b.CorridorId == corridorId && b.ServiceDate == serviceDate)
            .Join(
                context.Customers.AsNoTracking(),
                booking => booking.CustomerId,
                customer => customer.Id,
                (booking, customer) => new BookingRecipientRow(
                    booking.Id,
                    customer.Id,
                    customer.Name,
                    booking.Status,
                    customer.Email))
            .ToListAsync(cancellationToken);

    private static BookingResponse ToResponse(Domain.Bookings.Booking booking, DateTimeOffset now) => new(
        booking.Id,
        booking.CustomerId,
        booking.CustomerName,
        booking.CorridorId,
        booking.CorridorName,
        booking.ServiceDate,
        booking.Status,
        new BookingLocationResponse(booking.Pickup.StopId, booking.Pickup.StopName, booking.Pickup.AddressDetail),
        new BookingLocationResponse(booking.Dropoff.StopId, booking.Dropoff.StopName, booking.Dropoff.AddressDetail),
        [.. booking.Passengers.Select(p => new BookingPassengerResponse(p.Id, p.Name, p.Phone, p.IsBillingCustomer))],
        booking.PaymentMethod,
        booking.PaymentStatus,
        booking.HoldExpiresAtUtc,
        HoldExpired: booking.Status == BookingStatus.Unconfirmed && booking.HoldExpiresAtUtc <= now,
        booking.Notes,
        booking.CreatedAtUtc,
        booking.UpdatedAtUtc);
}
