using NorthernLink.Shared.Messaging;

namespace NorthernLink.Billing.Application.BillableTrips.GetBillableTrips;

/// <summary>Billable-trip pool listing, e.g. "uninvoiced trips for this client this month".</summary>
public sealed record GetBillableTripsQuery(
    Guid TenantId,
    Guid? ClientId,
    bool UninvoicedOnly,
    DateOnly? From,
    DateOnly? To) : IQuery<IReadOnlyList<BillableTripResponse>>;
