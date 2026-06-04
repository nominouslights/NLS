using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Queries.Drivers;

internal sealed class GetArchivedDriversQueryHandler(IDriverRepository driverRepository)
    : IRequestHandler<GetArchivedDriversQuery, IReadOnlyList<ArchivedDriverResult>>
{
    public async Task<IReadOnlyList<ArchivedDriverResult>> Handle(
        GetArchivedDriversQuery request,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddYears(-1);
        await driverRepository.PurgeExpiredAsync(cutoff, cancellationToken);

        var archived = await driverRepository.GetAllArchivedAsync(cancellationToken);

        return archived.Select(d => new ArchivedDriverResult(
            d.Id,
            d.EmployeeId,
            d.FirstName,
            d.LastName,
            d.FullName,
            d.Phone,
            d.Email,
            d.Status.ToString(),
            d.IsActive,
            d.DeletedAt
        )).ToList();
    }
}
