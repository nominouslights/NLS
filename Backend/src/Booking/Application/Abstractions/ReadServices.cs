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
/// bookings so a 31-day sweep stays light. <see cref="BookingId"/> lets the threshold
/// recompute overlay a not-yet-saved status change onto the row it just mutated (the
/// recompute runs inside the confirm/cancel transaction, so the DB still shows the old
/// status).
/// </summary>
public sealed record BookingSeatRow(
    Guid BookingId,
    DateOnly ServiceDate,
    BookingStatus Status,
    DateTimeOffset HoldExpiresAtUtc,
    int PassengerCount);

/// <summary>
/// One booking's customer contact for the revert notification snapshot. Per-booking (not
/// per-customer) so the threshold recompute can overlay the mutated booking's new status
/// before filtering to non-cancelled bookings; the handler dedupes customers afterwards.
/// <see cref="Email"/> is null/blank for customers with no email — they are excluded from
/// the snapshot, never a delivery failure.
/// </summary>
public sealed record BookingRecipientRow(
    Guid BookingId,
    Guid CustomerId,
    string CustomerName,
    BookingStatus Status,
    string? Email);

/// <summary>Read side for bookings — day panel detail, month seat rows, revert recipients.</summary>
public interface IBookingReadService
{
    /// <summary>All bookings (every status, cancelled included) for one corridor + date.</summary>
    Task<IReadOnlyList<BookingResponse>> GetForDateAsync(
        Guid corridorId, DateOnly serviceDate, CancellationToken cancellationToken = default);

    /// <summary>Seat rows for one corridor across a date range (inclusive), every status.</summary>
    Task<IReadOnlyList<BookingSeatRow>> GetSeatRowsAsync(
        Guid corridorId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Customer contact rows for every booking (any status) on one corridor + date — joined
    /// to the customer roster for the email. Feeds the reverted-day recipient snapshot.
    /// </summary>
    Task<IReadOnlyList<BookingRecipientRow>> GetRecipientRowsAsync(
        Guid corridorId, DateOnly serviceDate, CancellationToken cancellationToken = default);
}
