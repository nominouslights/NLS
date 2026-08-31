using NorthernLink.Shared.Messaging;

namespace NorthernLink.Booking.Application.Customers.Create;

public sealed record CreateCustomerCommand(
    Guid TenantId,
    string Name,
    string? Phone,
    string? Email,
    string? Notes) : ICommand<Guid>;
