using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Application.Manifests.GetManifests;

public sealed class GetTripManifestsQueryHandler(ITripManifestReadService readService)
    : IQueryHandler<GetTripManifestsQuery, IReadOnlyList<TripManifestResponse>>
{
    public async Task<Result<IReadOnlyList<TripManifestResponse>>> Handle(
        GetTripManifestsQuery query,
        CancellationToken cancellationToken)
    {
        var manifests = await readService.GetManifestsAsync(query.TripNumber, cancellationToken);
        return Result.Success(manifests);
    }
}
