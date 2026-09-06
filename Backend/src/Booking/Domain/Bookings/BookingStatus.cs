namespace NorthernLink.Booking.Domain.Bookings;

/// <summary>
/// The booking lifecycle. Transitions: Unconfirmed → Confirmed (only), and
/// {Unconfirmed, Confirmed} → Cancelled. Cancelled is terminal and read-only —
/// revert-not-delete keeps the record listed. "Hold expired" is deliberately NOT a
/// status: it is derived per read from <see cref="Booking.HoldExpiresAtUtc"/>, so an
/// expired hold simply stops reserving a seat without a background job flipping state.
/// </summary>
public enum BookingStatus
{
    Unconfirmed,
    Confirmed,
    Cancelled,
}
