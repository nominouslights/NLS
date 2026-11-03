using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Infrastructure.Persistence;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// Pins the jsonb key of every timetable offset inside the <c>stops</c> column. The departure
/// offsets were renamed in code but must keep their original stored keys; if these names
/// drift, every existing timetable silently reads back as untimed. The model is built from the
/// real <see cref="TripsDbContext"/> configuration — no database connection is opened.
/// </summary>
public class RouteStopJsonMappingTests
{
    public static TheoryData<Type> StopOwners => new()
    {
        typeof(Route),
        typeof(Trip),
        typeof(RouteReadModel),
        typeof(TripReadModel),
    };

    [Theory]
    [MemberData(nameof(StopOwners))]
    public void The_stops_json_keeps_its_stored_offset_keys(Type owner)
    {
        using var context = BuildContext();

        var stops = StopsEntity(context.Model, owner);

        Assert.Equal("stops", stops.GetContainerColumnName());
        Assert.Equal("OutboundOffsetMinutes", JsonName(stops, nameof(RouteStop.OutboundDepartureOffsetMinutes)));
        Assert.Equal("ReturnOffsetMinutes", JsonName(stops, nameof(RouteStop.ReturnDepartureOffsetMinutes)));
        Assert.Equal("OutboundArrivalOffsetMinutes", JsonName(stops, nameof(RouteStop.OutboundArrivalOffsetMinutes)));
        Assert.Equal("ReturnArrivalOffsetMinutes", JsonName(stops, nameof(RouteStop.ReturnArrivalOffsetMinutes)));
    }

    private static IEntityType StopsEntity(IModel model, Type owner)
    {
        var ownerEntity = model.FindEntityType(owner)
            ?? throw new InvalidOperationException($"{owner.Name} is not in the Trips model.");
        var navigation = ownerEntity.FindNavigation("Stops")
            ?? throw new InvalidOperationException($"{owner.Name} has no Stops navigation.");
        return navigation.TargetEntityType;
    }

    private static string? JsonName(IEntityType stops, string property) =>
        (stops.FindProperty(property)
            ?? throw new InvalidOperationException($"{property} is not mapped on {stops.DisplayName()}."))
        .GetJsonPropertyName();

    private static TripsDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<TripsDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only")
            .Options;
        return new TripsDbContext(options, new NoTenant());
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantType? TenantType => null;
    }
}
