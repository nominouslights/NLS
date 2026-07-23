using Microsoft.EntityFrameworkCore;
using NorthernLink.Billing.Application.Abstractions;
using NorthernLink.Billing.Application.BillableTrips;
using NorthernLink.Billing.Domain.BillableTrips;

namespace NorthernLink.Billing.Infrastructure.Persistence;

/// <summary>
/// Read side — <c>billing.billable_trips</c> queried directly (tenant-filtered): the
/// replica is already flat, so it needs no rm_* projection.
/// </summary>
internal sealed class BillableTripReadService(BillingDbContext context) : IBillableTripReadService
{
    public async Task<IReadOnlyList<BillableTripResponse>> GetBillableTripsAsync(
        Guid? clientId,
        bool uninvoicedOnly,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var query = context.BillableTrips.AsNoTracking();

        if (clientId is { } client)
        {
            query = query.Where(t => t.ClientId == client);
        }

        if (uninvoicedOnly)
        {
            query = query.Where(t => t.InvoiceId == null);
        }

        if (from is { } fromDate)
        {
            query = query.Where(t => t.ServiceDate >= fromDate);
        }

        if (to is { } toDate)
        {
            query = query.Where(t => t.ServiceDate <= toDate);
        }

        var trips = await query
            .OrderByDescending(t => t.ServiceDate)
            .ThenBy(t => t.TripNumber)
            .ToListAsync(cancellationToken);

        return trips.Select(ToResponse).ToList();
    }

    private static BillableTripResponse ToResponse(BillableTrip t) => new(
        t.Id,
        t.TripNumber,
        t.ClientId,
        t.ClientName,
        t.ServiceType,
        t.RouteName,
        t.Origin,
        t.Destination,
        t.DistanceKm,
        t.ServiceDate,
        t.RoundTripKey,
        t.PoNumber,
        t.CompletedAtUtc,
        t.InvoiceId);
}
