namespace NorthernLink.Trips.Application.Routes;

/// <summary>
/// One stop as supplied when creating or updating a route: the catalog stop to snapshot, plus
/// that stop's optional timetable offsets (an arrival and a departure per leg). Position in the list is the corridor order.
/// </summary>
/// <param name="StopId">Catalog stop to resolve and snapshot (name + coordinates).</param>
/// <param name="OutboundDepartureOffsetMinutes">
/// Minutes after the outbound leg's departure that the vehicle leaves this stop (the pickup
/// time). Null on every stop = the route has no outbound timetable; a leg's departures are
/// all-or-nothing, and <c>Route.Create</c>/<c>Update</c> rejects a partial set.
/// </param>
/// <param name="ReturnDepartureOffsetMinutes">
/// Minutes after the return leg's departure that the vehicle leaves this stop. The return runs
/// the corridor backwards, so the <em>last</em> stop in this list is the return's zero.
/// </param>
/// <param name="OutboundArrivalOffsetMinutes">
/// Minutes after the outbound leg's departure that the vehicle reaches this stop (the drop-off
/// time). Optional per stop — null means it arrives when it departs.
/// </param>
/// <param name="ReturnArrivalOffsetMinutes">As <paramref name="OutboundArrivalOffsetMinutes"/>, return leg.</param>
public sealed record RouteStopInput(
    Guid StopId,
    int? OutboundDepartureOffsetMinutes = null,
    int? ReturnDepartureOffsetMinutes = null,
    int? OutboundArrivalOffsetMinutes = null,
    int? ReturnArrivalOffsetMinutes = null);
