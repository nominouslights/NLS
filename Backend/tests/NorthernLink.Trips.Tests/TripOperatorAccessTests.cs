using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Trips;
using NorthernLink.Trips.Application.Trips.GetActivity;
using NorthernLink.Trips.Application.Trips.GetTripById;
using NorthernLink.Trips.Application.Trips.GetTrips;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The read half of the identity gate on the driver-facing <c>/api/trips</c> routes: the list
/// narrows to the caller's own driver, and the detail/activity routes answer
/// <c>Trips.Trip.NotYourTrip</c> for anyone else's trip. (The status command's gate is tested
/// beside the status rules in <see cref="ChangeTripStatusCommandHandlerTests"/>.)
/// </summary>
public class TripOperatorAccessTests
{
    private static readonly Guid DriverAUserId = Guid.Parse("00000000-0000-0000-0000-00000000ab01");
    private static readonly Guid DriverBUserId = Guid.Parse("00000000-0000-0000-0000-00000000ab02");
    private static readonly Guid DriverBId = Guid.Parse("00000000-0000-0000-0000-0000000000d2");

    private readonly FakeDriverLookupRepository _drivers = new();
    private readonly FakeTripReadService _reads = new();
    private readonly FakeTripRepository _trips = new();
    private readonly FakeTripActivityReadService _activity = new();

    public TripOperatorAccessTests()
    {
        _drivers.Drivers.Add(TestPlanning.ActiveDriver(userId: DriverAUserId));
        _drivers.Drivers.Add(TestPlanning.ActiveDriver(userId: DriverBUserId, driverId: DriverBId));
    }

    private TripOperatorAccess Access(FakeCurrentActor actor) => new(actor, _drivers);

    // ---- Scope resolution ----

    [Fact]
    public async Task Dispatch_is_unrestricted_without_a_lookup()
    {
        var scope = await Access(FakeCurrentActor.Dispatcher).ResolveScopeAsync();

        Assert.True(scope.IsUnrestricted);
        Assert.Equal(0, _drivers.UserLookups);
    }

    [Fact]
    public async Task A_linked_driver_is_scoped_to_their_own_driver_row()
    {
        var scope = await Access(FakeCurrentActor.Driver(DriverBUserId)).ResolveScopeAsync();

        Assert.False(scope.IsUnrestricted);
        Assert.Equal(DriverBId, scope.DriverId);
    }

    [Fact]
    public async Task An_unlinked_driver_account_and_a_missing_principal_see_nothing()
    {
        Assert.Equal(TripOperatorScope.Nothing, await Access(FakeCurrentActor.Driver(Guid.NewGuid())).ResolveScopeAsync());
        Assert.Equal(TripOperatorScope.Nothing, await Access(new FakeCurrentActor(null, Roles.Driver)).ResolveScopeAsync());
    }

    [Fact]
    public async Task An_unassigned_trip_belongs_to_dispatch_only()
    {
        Assert.True(await Access(FakeCurrentActor.Dispatcher).MayOperateAsync(null));
        Assert.False(await Access(FakeCurrentActor.Driver(DriverAUserId)).MayOperateAsync(null));
    }

    // ---- GET /api/trips ----

    private GetTripsQueryHandler ListHandler(FakeCurrentActor actor) => new(_reads, Access(actor));

    private void SeedOneTripEach()
    {
        _reads.Trips.Add(Response(Guid.NewGuid(), TestPlanning.DriverId));
        _reads.Trips.Add(Response(Guid.NewGuid(), DriverBId));
    }

    [Fact]
    public async Task The_list_is_narrowed_to_the_callers_own_driver()
    {
        SeedOneTripEach();

        var result = await ListHandler(FakeCurrentActor.Driver(DriverAUserId)).Handle(
            new GetTripsQuery(TestPlanning.TenantId, new TripFilter()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var only = Assert.Single(result.Value);
        Assert.Equal(TestPlanning.DriverId, only.DriverId);
        Assert.Equal(TestPlanning.DriverId, _reads.LastFilter!.DriverId);
        Assert.Equal(1, result.PageInfo!.TotalCount);
    }

    [Fact]
    public async Task A_driver_asking_for_another_drivers_history_gets_an_empty_page_not_theirs()
    {
        // ?driverId= is ANDed with ownership, never overridden to the caller's own id — a wrong
        // answer silently labelled as the right one is worse than an empty one.
        SeedOneTripEach();

        var result = await ListHandler(FakeCurrentActor.Driver(DriverAUserId)).Handle(
            new GetTripsQuery(TestPlanning.TenantId, new TripFilter(DriverId: DriverBId, Page: 1, PageSize: 50)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Equal(0, result.PageInfo!.TotalCount);
        Assert.Null(_reads.LastFilter); // the read side was never asked
    }

    [Fact]
    public async Task An_unlinked_driver_account_lists_nothing()
    {
        SeedOneTripEach();

        var result = await ListHandler(FakeCurrentActor.Driver(Guid.NewGuid())).Handle(
            new GetTripsQuery(TestPlanning.TenantId, new TripFilter()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Null(_reads.LastFilter);
    }

    [Fact]
    public async Task Dispatch_lists_everything_with_the_filter_untouched()
    {
        SeedOneTripEach();
        var filter = new TripFilter(Date: new DateOnly(2026, 7, 21), ExcludeCancelled: true);

        var result = await ListHandler(FakeCurrentActor.Dispatcher).Handle(
            new GetTripsQuery(TestPlanning.TenantId, filter), CancellationToken.None);

        Assert.Equal(2, result.Value.Count);
        Assert.Same(filter, _reads.LastFilter);
    }

    // ---- GET /api/trips/{id} ----

    [Fact]
    public async Task Detail_of_another_drivers_trip_is_NotYourTrip_not_NotFound()
    {
        var theirs = Response(Guid.NewGuid(), DriverBId);
        _reads.Trips.Add(theirs);

        var result = await new GetTripByIdQueryHandler(_reads, Access(FakeCurrentActor.Driver(DriverAUserId))).Handle(
            new GetTripByIdQuery(TestPlanning.TenantId, theirs.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.NotYourTrip, result.Error);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public async Task Detail_of_the_callers_own_trip_and_of_any_trip_for_dispatch_succeeds()
    {
        var mine = Response(Guid.NewGuid(), TestPlanning.DriverId);
        _reads.Trips.Add(mine);

        var own = await new GetTripByIdQueryHandler(_reads, Access(FakeCurrentActor.Driver(DriverAUserId))).Handle(
            new GetTripByIdQuery(TestPlanning.TenantId, mine.Id), CancellationToken.None);
        var dispatch = await new GetTripByIdQueryHandler(_reads, Access(new FakeCurrentActor(Guid.NewGuid(), Roles.Owner))).Handle(
            new GetTripByIdQuery(TestPlanning.TenantId, mine.Id), CancellationToken.None);

        Assert.True(own.IsSuccess);
        Assert.True(dispatch.IsSuccess);
    }

    [Fact]
    public async Task An_unknown_trip_is_still_NotFound_for_a_driver()
    {
        var result = await new GetTripByIdQueryHandler(_reads, Access(FakeCurrentActor.Driver(DriverAUserId))).Handle(
            new GetTripByIdQuery(TestPlanning.TenantId, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(TripErrors.NotFound, result.Error);
    }

    // ---- GET /api/trips/{id}/activity ----

    [Fact]
    public async Task Activity_of_another_drivers_trip_is_NotYourTrip_and_the_journal_is_never_read()
    {
        var theirs = TestPlanning.ScheduleTrip(driverId: DriverBId).Value;
        _trips.Add(theirs);

        var result = await new GetTripActivityQueryHandler(_trips, _activity, Access(FakeCurrentActor.Driver(DriverAUserId))).Handle(
            new GetTripActivityQuery(theirs.Id, TestPlanning.TenantId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.NotYourTrip, result.Error);
        Assert.Null(_activity.RequestedTripId);
    }

    [Fact]
    public async Task Activity_of_the_callers_own_trip_succeeds()
    {
        var mine = TestPlanning.ScheduleTrip().Value;
        _trips.Add(mine);

        var result = await new GetTripActivityQueryHandler(_trips, _activity, Access(FakeCurrentActor.Driver(DriverAUserId))).Handle(
            new GetTripActivityQuery(mine.Id, TestPlanning.TenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(mine.Id, _activity.RequestedTripId);
    }

    private static TripResponse Response(Guid id, Guid? driverId) => new(
        id, "TR-1001", new DateOnly(2026, 7, 21), new TimeOnly(6, 30), new TimeOnly(8, 15), "ContractCrew",
        null, "Thompson ↔ Lynn Lake", "Thompson", "Lynn Lake", [], 320, null, null, null, false,
        null, "Alamos Gold", "PO-2026-118", driverId, TestPlanning.DriverName, TestPlanning.VehicleId,
        TestPlanning.VehicleUnit, 12, 0, null, false, "Scheduled", null, false, null, null, null, null,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
}
