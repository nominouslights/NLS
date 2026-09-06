using NorthernLink.Shared.Kernel;

namespace NorthernLink.Booking.Domain.Customers;

/// <summary>All domain errors the Customer aggregate (and its handlers) can produce.</summary>
public static class CustomerErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Booking.Customer.NotFound", "The customer was not found.");

    public static readonly Error NameRequired = Error.Validation(
        "Booking.Customer.NameRequired", "A customer name is required.");
}
