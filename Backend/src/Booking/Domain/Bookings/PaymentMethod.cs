namespace NorthernLink.Booking.Domain.Bookings;

/// <summary>
/// How the customer intends to pay (or paid). A plain recorded field in this batch —
/// no processor integration; Square/e-Transfer webhooks are a later batch.
/// </summary>
public enum PaymentMethod
{
    Square,
    ETransfer,
    Cash,
}
