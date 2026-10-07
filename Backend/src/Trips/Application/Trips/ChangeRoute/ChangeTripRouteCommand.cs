using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.ChangeRoute;

/// <summary>
/// Moves a Scheduled trip — and its paired leg, oriented for that leg's direction — onto another
/// catalogue route, in one save. Re-runs <see cref="TripRouteChangeImpactCalculator"/>: the first
/// blocker is returned as the error; outstanding warnings without
/// <see cref="AcknowledgeWarnings"/> return <c>Trips.Trip.RouteChangeNeedsAcknowledgement</c>.
/// </summary>
public sealed record ChangeTripRouteCommand(Guid TripId, Guid RouteId, bool AcknowledgeWarnings) : ICommand;
