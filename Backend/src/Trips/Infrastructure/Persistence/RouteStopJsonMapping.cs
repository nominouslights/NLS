using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NorthernLink.Trips.Domain.Routes;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// The one place the <c>stops</c> jsonb shape of a <see cref="RouteStop"/> list is mapped —
/// routes, trips and their read-model mirrors (rm_routes, rm_trips) all call this, so the write
/// and read sides can never drift.
/// <para>
/// The departure offsets were the only timetable offsets before arrivals existed and are still
/// stored under their original keys (<c>OutboundOffsetMinutes</c> / <c>ReturnOffsetMinutes</c>):
/// every existing timetable reads unchanged and no jsonb rewrite is needed. Changing these
/// names silently drops every stored timetable — <c>RouteStopJsonMappingTests</c> pins them.
/// </para>
/// </summary>
internal static class RouteStopJsonMapping
{
    public static void Configure<TOwner>(this OwnedNavigationBuilder<TOwner, RouteStop> stop)
        where TOwner : class
    {
        stop.ToJson("stops");
        stop.Property(s => s.OutboundDepartureOffsetMinutes).HasJsonPropertyName("OutboundOffsetMinutes");
        stop.Property(s => s.ReturnDepartureOffsetMinutes).HasJsonPropertyName("ReturnOffsetMinutes");
    }
}
