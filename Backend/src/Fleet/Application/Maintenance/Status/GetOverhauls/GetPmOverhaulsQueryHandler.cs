using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Maintenance.Status.GetOverhauls;

public sealed class GetPmOverhaulsQueryHandler(IPmReadService readService)
    : IQueryHandler<GetPmOverhaulsQuery, PmOverhaulsResponse>
{
    public async Task<Result<PmOverhaulsResponse>> Handle(
        GetPmOverhaulsQuery query,
        CancellationToken cancellationToken)
    {
        var overhauls = await readService.GetOverhaulsAsync(query.VehicleId, cancellationToken);
        return overhauls is null
            ? Result.Failure<PmOverhaulsResponse>(VehicleErrors.NotFound)
            : Result.Success(overhauls);
    }
}
