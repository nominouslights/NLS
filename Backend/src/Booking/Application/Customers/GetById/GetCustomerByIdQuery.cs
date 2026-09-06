using NorthernLink.Shared.Messaging;

namespace NorthernLink.Booking.Application.Customers.GetById;

public sealed record GetCustomerByIdQuery(Guid TenantId, Guid CustomerId) : IQuery<CustomerResponse>;
