namespace ShuttleApi.Domain.Trips;

public enum PassengerPaymentStatus
{
    Tentative,       // booked, seat held, no payment collected or requested
    AwaitingPayment, // cutoff window opened, payment request sent to passenger
    Confirmed,       // paid (online or cash at booth)
    Released,        // cutoff passed without payment — seat returned to pool
    Cancelled        // trip cancelled or operator block applied
}
