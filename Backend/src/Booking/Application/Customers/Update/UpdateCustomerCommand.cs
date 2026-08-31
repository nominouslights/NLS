using NorthernLink.Shared.Messaging;

namespace NorthernLink.Booking.Application.Customers.Update;

public sealed record UpdateCustomerCommand(
    Guid TenantId,
    Guid CustomerId,
    string Name,
    string? Phone,
    string? Email,
    string? Notes) : ICommand;
