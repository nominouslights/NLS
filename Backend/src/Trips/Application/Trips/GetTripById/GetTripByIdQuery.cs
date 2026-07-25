using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.GetTripById;

/// <summary>Trip detail (includes the attached manifest id, when any).</summary>
public sealed record GetTripByIdQuery(Guid TenantId, Guid TripId) : IQuery<TripResponse>;
