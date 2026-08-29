using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Riders;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Read side — queries <c>trips.rm_riders</c> and maps to the public contract. The
/// grouped order (ContractCrew → Community → Nihb → Charter, then name) and the computed
/// NextExpectedTravelDate (<c>LastTripDate + RotationDays</c>) are applied here, in
/// memory: service_type is stored as its enum name, the enum's declaration order IS the
/// display order, and a tenant's directory is small.
/// </summary>
internal sealed class RiderReadService(TripsDbContext context) : IRiderReadService
{
    public async Task<IReadOnlyList<RiderResponse>> GetRidersAsync(
        TripServiceType? serviceType = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.RiderReadModels.AsNoTracking();

        if (serviceType is { } filter)
        {
            var name = filter.ToString();
            query = query.Where(r => r.ServiceType == name);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(r => EF.Functions.ILike(r.Name, term));
        }

        var rows = await query.ToListAsync(cancellationToken);

        return rows
            .Select(r =>
            {
                var parsedType = Enum.Parse<TripServiceType>(r.ServiceType);
                return new RiderResponse(
                    r.Id,
                    r.Name,
                    parsedType,
                    r.Contact,
                    r.RotationDays,
                    r.LastTripDate,
                    r.LastTripNumber,
                    r.LastTripDate is { } last && r.RotationDays is { } rotation
                        ? last.AddDays(rotation)
                        : null,
                    r.TripCount,
                    r.CreatedAtUtc,
                    r.UpdatedAtUtc);
            })
            .OrderBy(r => (int)r.ServiceType)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
