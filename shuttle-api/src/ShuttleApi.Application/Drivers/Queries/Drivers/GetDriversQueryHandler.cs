using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Queries.Drivers;

internal sealed class GetDriversQueryHandler(IDriverRepository driverRepository)
    : IRequestHandler<GetDriversQuery, IReadOnlyList<DriverListItemResult>>
{
    public async Task<IReadOnlyList<DriverListItemResult>> Handle(
        GetDriversQuery request,
        CancellationToken cancellationToken)
    {
        var drivers = await driverRepository.GetAllAsync(cancellationToken);

        return drivers.Select(d => new DriverListItemResult(
            d.Id,
            d.EmployeeId,
            d.FirstName,
            d.LastName,
            d.FullName,
            d.Phone,
            d.Email,
            d.Status.ToString(),
            d.IsActive,
            d.Documents.Any(doc => doc.IsExpiringSoon)
        )).ToList();
    }
}
