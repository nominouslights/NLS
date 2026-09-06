namespace NorthernLink.Booking.Domain.Bookings;

/// <summary>
/// Whether payment has been received. A plain recorded field the dispatcher sets by
/// hand in this batch — payment-processor reconciliation is a later batch.
/// </summary>
public enum PaymentStatus
{
    Unpaid,
    Paid,
}
