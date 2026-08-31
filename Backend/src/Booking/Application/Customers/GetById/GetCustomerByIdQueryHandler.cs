using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Domain.Customers;

namespace NorthernLink.Booking.Application.Customers.GetById;

public sealed class GetCustomerByIdQueryHandler(ICustomerReadService readService)
    : IQueryHandler<GetCustomerByIdQuery, CustomerResponse>
{
    public async Task<Result<CustomerResponse>> Handle(
        GetCustomerByIdQuery query, CancellationToken cancellationToken)
    {
        var customer = await readService.GetByIdAsync(query.CustomerId, cancellationToken);
        return customer is null
            ? Result.Failure<CustomerResponse>(CustomerErrors.NotFound)
            : Result.Success(customer);
    }
}
