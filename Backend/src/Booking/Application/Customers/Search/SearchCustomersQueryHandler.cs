using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;

namespace NorthernLink.Booking.Application.Customers.Search;

public sealed class SearchCustomersQueryHandler(ICustomerReadService readService)
    : IQueryHandler<SearchCustomersQuery, IReadOnlyList<CustomerResponse>>
{
    public async Task<Result<IReadOnlyList<CustomerResponse>>> Handle(
        SearchCustomersQuery query, CancellationToken cancellationToken)
    {
        var customers = await readService.SearchAsync(query.Search, cancellationToken);
        return Result.Success(customers);
    }
}
