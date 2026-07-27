using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Stops;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>Read side — queries <c>trips.rm_stops</c> and maps to the public contract.</summary>
internal sealed class StopReadService(TripsDbContext context) : IStopReadService
{
    public async Task<IReadOnlyList<StopResponse>> GetStopsAsync(CancellationToken cancellationToken = default)
    {
        var stops = await context.StopReadModels
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return stops.Select(s => new StopResponse(
            s.Id,
            s.Name,
            s.Type,
            s.Street,
            s.City,
            s.Province,
            s.PostalCode,
            s.Country,
            s.Latitude,
            s.Longitude,
            s.Notes,
            s.Active,
            s.CreatedAtUtc,
            s.UpdatedAtUtc)).ToList();
    }
}
