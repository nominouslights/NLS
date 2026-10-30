using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Shared.IntegrationEvents.Drivers;
using NorthernLink.Trips.Application.Integration;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The replica upsert that feeds the identity gate: <c>driver_lookup.user_id</c> must follow
/// the event exactly — set on link, cleared on unlink — or a revoked account keeps acting on
/// that driver's trips.
/// </summary>
public class DriverChangedIntegrationEventHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-00000000ab01");

    private readonly FakeDriverLookupRepository _drivers = new();

    private DriverChangedIntegrationEventHandler Handler =>
        new(_drivers, NullLogger<DriverChangedIntegrationEventHandler>.Instance);

    private static DriverChangedIntegrationEvent Event(Guid? userId, string status = "Active") => new(
        TestPlanning.DriverId, TestPlanning.TenantId, TestPlanning.DriverName, "Class 4", status, "Northern Link", userId);

    [Fact]
    public async Task Stores_the_linked_user_id_on_the_replica_row()
    {
        await Handler.Handle(Event(UserId), CancellationToken.None);

        var row = Assert.Single(_drivers.Drivers);
        Assert.Equal(TestPlanning.DriverId, row.DriverId);
        Assert.Equal(TestPlanning.TenantId, row.TenantId);
        Assert.Equal(UserId, row.UserId);
        Assert.True(row.IsActive);
        Assert.Same(row, await _drivers.GetByUserIdAsync(UserId));
    }

    [Fact]
    public async Task An_unlink_clears_the_user_id_rather_than_keeping_the_old_one()
    {
        await Handler.Handle(Event(UserId), CancellationToken.None);

        await Handler.Handle(Event(userId: null), CancellationToken.None);

        var row = Assert.Single(_drivers.Drivers);
        Assert.Null(row.UserId);
        Assert.Null(await _drivers.GetByUserIdAsync(UserId));
    }

    [Fact]
    public async Task A_legacy_event_without_a_user_id_upserts_with_null()
    {
        // Pre-UserId outbox rows replay with the member defaulted — the row still lands, unlinked.
        var legacy = new DriverChangedIntegrationEvent(
            TestPlanning.DriverId, TestPlanning.TenantId, TestPlanning.DriverName, "Class 4", "Active", "Northern Link");

        await Handler.Handle(legacy, CancellationToken.None);

        Assert.Null(Assert.Single(_drivers.Drivers).UserId);
    }
}
