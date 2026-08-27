namespace NorthernLink.Trips.Application.Routes;

/// <summary>
/// One stop as supplied when creating or updating a route: the catalog stop to snapshot, plus
/// that stop's optional timetable offsets. Position in the list is the corridor order.
/// </summary>
/// <param name="StopId">Catalog stop to resolve and snapshot (name + coordinates).</param>
/// <param name="OutboundOffsetMinutes">
/// Minutes after the outbound leg's departure. Null on every stop = the route has no outbound
/// timetable; a leg is all-or-nothing, and <c>Route.Create</c>/<c>Update</c> rejects a partial one.
/// </param>
/// <param name="ReturnOffsetMinutes">
/// Minutes after the return leg's departure. The return runs the corridor backwards, so the
/// <em>last</em> stop in this list is the return's zero.
/// </param>
public sealed record RouteStopInput(
    Guid StopId,
    int? OutboundOffsetMinutes = null,
    int? ReturnOffsetMinutes = null);
