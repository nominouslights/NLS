using NorthernLink.Billing.Application.BillableTrips;

namespace NorthernLink.Billing.Application.Abstractions;

/// <summary>Read side — <c>billable_trips</c> is already a flat replica table, queried
/// directly (no rm_* projection needed).</summary>
public interface IBillableTripReadService
{
    Task<IReadOnlyList<BillableTripResponse>> GetBillableTripsAsync(
        Guid? clientId,
        bool uninvoicedOnly,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);
}
