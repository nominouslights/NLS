namespace NorthernLink.Booking.Domain.BookingDays;

/// <summary>
/// The lifecycle of one corridor + date's booking demand. Confirmed/Unconfirmed live HERE,
/// not on the Trip: the trip created at confirmation stays <c>Scheduled</c> throughout
/// (derived states stay out of Trip persistence), and "reverting the trip to Unconfirmed"
/// is represented by this status, which the UI joins in. Machine: Unconfirmed → Confirmed
/// (threshold crossed up, or Gift-a-Seat), Confirmed → Reverted (dropped below minimum
/// outside the cancellation window, minimum not guaranteed), Reverted → Confirmed
/// (bookings recover the minimum, or Gift-a-Seat). Revert-not-delete: a Reverted day keeps
/// its bookings and its trip link.
/// </summary>
public enum BookingDayStatus
{
    Unconfirmed,
    Confirmed,
    Reverted,
}
