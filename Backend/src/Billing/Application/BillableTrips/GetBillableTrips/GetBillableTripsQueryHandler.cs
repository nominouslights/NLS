using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.BillableTrips.GetBillableTrips;

public sealed class GetBillableTripsQueryHandler(IBillableTripReadService readService)
    : IQueryHandler<GetBillableTripsQuery, IReadOnlyList<BillableTripResponse>>
{
    public async Task<Result<IReadOnlyList<BillableTripResponse>>> Handle(
        GetBillableTripsQuery query,
        CancellationToken cancellationToken)
    {
        var trips = await readService.GetBillableTripsAsync(
            query.ClientId, query.UninvoicedOnly, query.From, query.To, cancellationToken);
        return Result.Success(trips);
    }
}
