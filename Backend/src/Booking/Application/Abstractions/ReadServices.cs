using NorthernLink.Booking.Application.Bookings;
using NorthernLink.Booking.Application.Customers;
using NorthernLink.Booking.Domain.Bookings;

namespace NorthernLink.Booking.Application.Abstractions;

/// <summary>Read side for customers — search is name substring OR digits-only phone substring.</summary>
public interface ICustomerReadService
{
    Task<IReadOnlyList<CustomerResponse>> SearchAsync(string? search, CancellationToken cancellationToken = default);

    Task<CustomerResponse?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// One booking's contribution to a day's seat math: its status, when its hold lapses, and
/// how many seats (passenger rows) it carries. The month query reads these instead of full
/// bookings so a 31-day sweep stays light.
/// </summary>
public sealed record BookingSeatRow(
    DateOnly ServiceDate,
    BookingStatus Status,
    DateTimeOffset HoldExpiresAtUtc,
    int PassengerCount);

/// <summary>Read side for bookings — day panel detail and month seat rows.</summary>
public interface IBookingReadService
{
    /// <summary>All bookings (every status, cancelled included) for one corridor + date.</summary>
    Task<IReadOnlyList<BookingResponse>> GetForDateAsync(
        Guid corridorId, DateOnly serviceDate, CancellationToken cancellationToken = default);

    /// <summary>Seat rows for one corridor across a date range (inclusive), every status.</summary>
    Task<IReadOnlyList<BookingSeatRow>> GetSeatRowsAsync(
        Guid corridorId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
