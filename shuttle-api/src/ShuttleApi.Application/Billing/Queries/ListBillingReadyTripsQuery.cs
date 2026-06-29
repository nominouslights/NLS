using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Billing;

public sealed record ListBillingReadyTripsQuery(Guid ClientId) : IQuery<List<BillingReadyTripResponse>>;
