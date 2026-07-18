using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Application.Manifests.GetById;

public sealed class GetTripManifestByIdQueryHandler(ITripManifestReadService readService)
    : IQueryHandler<GetTripManifestByIdQuery, TripManifestResponse>
{
    public async Task<Result<TripManifestResponse>> Handle(
        GetTripManifestByIdQuery query,
        CancellationToken cancellationToken)
    {
        var manifest = await readService.GetManifestAsync(query.ManifestId, cancellationToken);

        return manifest is null
            ? Result.Failure<TripManifestResponse>(TripManifestErrors.NotFound)
            : Result.Success(manifest);
    }
}
