using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;

namespace NorthernLink.Booking.Application.Corridors.GetCorridors;

public sealed class GetCorridorsQueryHandler(ICorridorLookupRepository corridors)
    : IQueryHandler<GetCorridorsQuery, IReadOnlyList<CorridorResponse>>
{
    public async Task<Result<IReadOnlyList<CorridorResponse>>> Handle(
        GetCorridorsQuery query, CancellationToken cancellationToken)
    {
        var lookups = await corridors.GetAllAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CorridorResponse>>(
            [.. lookups
                .OrderBy(c => c.Name)
                .Select(c => new CorridorResponse(c.CorridorId, c.Name, c.Origin, c.Destination, c.Active))]);
    }
}
