using NorthernLink.Fleet.Application.Vehicles;
using NorthernLink.Fleet.Application.Vehicles.SetMotiveId;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Domain.Vehicles.Events;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The per-vehicle Motive (ELD) link — the "regulated" flag. Set, clear, validation, the no-op on
/// an unchanged value, per-tenant uniqueness in the handler, and the response carrying it.
/// </summary>
public class VehicleMotiveIdTests
{
    private static Vehicle NewVehicle(string unitNumber = "U-99", string vin = TestVehicles.ValidVin)
    {
        var vehicle = TestVehicles.Create(unitNumber: unitNumber, vin: vin);
        vehicle.ClearDomainEvents();
        return vehicle;
    }

    // ---- Domain ----

    [Fact]
    public void A_newly_registered_vehicle_is_not_regulated()
    {
        var vehicle = NewVehicle();

        Assert.Null(vehicle.MotiveVehicleId);
        Assert.False(vehicle.IsRegulated);
    }

    [Fact]
    public void Setting_trims_marks_regulated_and_raises_its_own_event()
    {
        var vehicle = NewVehicle();

        var result = vehicle.SetMotiveVehicleId("  4821  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("4821", vehicle.MotiveVehicleId);
        Assert.True(vehicle.IsRegulated);
        var raised = Assert.IsType<VehicleMotiveIdChangedDomainEvent>(Assert.Single(vehicle.DomainEvents));
        Assert.Null(raised.PreviousMotiveVehicleId);
        Assert.Equal("4821", raised.MotiveVehicleId);
        Assert.Equal(vehicle.TenantId, raised.TenantId);
    }

    [Fact]
    public void Setting_the_same_value_again_is_a_no_op_without_an_event()
    {
        var vehicle = NewVehicle();
        vehicle.SetMotiveVehicleId("4821");
        vehicle.ClearDomainEvents();

        var result = vehicle.SetMotiveVehicleId(" 4821 ");

        Assert.True(result.IsSuccess);
        Assert.Empty(vehicle.DomainEvents);
    }

    [Fact]
    public void Clearing_removes_the_link_and_records_the_previous_value()
    {
        var vehicle = NewVehicle();
        vehicle.SetMotiveVehicleId("4821");
        vehicle.ClearDomainEvents();

        var result = vehicle.SetMotiveVehicleId(null);

        Assert.True(result.IsSuccess);
        Assert.Null(vehicle.MotiveVehicleId);
        Assert.False(vehicle.IsRegulated);
        var raised = Assert.IsType<VehicleMotiveIdChangedDomainEvent>(Assert.Single(vehicle.DomainEvents));
        Assert.Equal("4821", raised.PreviousMotiveVehicleId);
        Assert.Null(raised.MotiveVehicleId);
    }

    [Fact]
    public void Clearing_an_unlinked_vehicle_is_a_no_op() =>
        Assert.True(NewVehicle().SetMotiveVehicleId(null).IsSuccess);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_value_is_refused_rather_than_read_as_clear(string value)
    {
        var vehicle = NewVehicle();
        vehicle.SetMotiveVehicleId("4821");

        var result = vehicle.SetMotiveVehicleId(value);

        Assert.Equal(VehicleErrors.MotiveVehicleIdRequired, result.Error);
        Assert.Equal("4821", vehicle.MotiveVehicleId);
    }

    [Fact]
    public void The_length_cap_counts_the_trimmed_value()
    {
        var atCap = new string('7', Vehicle.MotiveVehicleIdMaxLength);

        Assert.True(NewVehicle().SetMotiveVehicleId($"  {atCap}  ").IsSuccess);
        Assert.Equal(
            VehicleErrors.MotiveVehicleIdTooLong,
            NewVehicle().SetMotiveVehicleId(atCap + "7").Error);
    }

    [Fact]
    public void A_disposed_vehicle_cannot_be_linked_but_can_be_cleared()
    {
        var vehicle = TestVehicles.InStatus(VehicleStatus.Retired);
        vehicle.SetMotiveVehicleId("4821");
        vehicle.Dispose(DisposalMethod.Recycled);

        Assert.Equal(VehicleErrors.Disposed, vehicle.SetMotiveVehicleId("9999").Error);
        Assert.True(vehicle.SetMotiveVehicleId(null).IsSuccess);
        Assert.Null(vehicle.MotiveVehicleId);
    }

    // ---- Handler ----

    [Fact]
    public async Task Unknown_vehicle_is_not_found()
    {
        var handler = new SetVehicleMotiveIdCommandHandler(new InMemoryVehicleRepository());

        var result = await handler.Handle(
            new SetVehicleMotiveIdCommand(TestVehicles.TenantId, Guid.NewGuid(), "4821"), CancellationToken.None);

        Assert.Equal(VehicleErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Handler_sets_and_saves()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = NewVehicle();
        repository.Add(vehicle);

        var result = await new SetVehicleMotiveIdCommandHandler(repository).Handle(
            new SetVehicleMotiveIdCommand(TestVehicles.TenantId, vehicle.Id, "4821"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("4821", vehicle.MotiveVehicleId);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_motive_id_already_on_another_vehicle_is_a_conflict_and_nothing_saves()
    {
        var repository = new InMemoryVehicleRepository();
        var first = NewVehicle("U-1", "1HVBBAAN8RH553310");
        first.SetMotiveVehicleId("4821");
        var second = NewVehicle("U-2", "2HGFC2F59JH542841");
        repository.Add(first);
        repository.Add(second);

        // Padded on purpose: the collision is checked on the trimmed value.
        var result = await new SetVehicleMotiveIdCommandHandler(repository).Handle(
            new SetVehicleMotiveIdCommand(TestVehicles.TenantId, second.Id, " 4821 "), CancellationToken.None);

        Assert.Equal(VehicleErrors.DuplicateMotiveVehicleId, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Clearing_never_collides_and_re_setting_its_own_value_is_not_a_conflict()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = NewVehicle();
        vehicle.SetMotiveVehicleId("4821");
        repository.Add(vehicle);
        var handler = new SetVehicleMotiveIdCommandHandler(repository);

        var again = await handler.Handle(
            new SetVehicleMotiveIdCommand(TestVehicles.TenantId, vehicle.Id, "4821"), CancellationToken.None);
        var cleared = await handler.Handle(
            new SetVehicleMotiveIdCommand(TestVehicles.TenantId, vehicle.Id, null), CancellationToken.None);

        Assert.True(again.IsSuccess);
        Assert.True(cleared.IsSuccess);
        Assert.Null(vehicle.MotiveVehicleId);
    }

    // ---- Contract ----

    [Fact]
    public void The_response_carries_the_motive_vehicle_id()
    {
        var vehicle = NewVehicle();
        Assert.Null(VehicleResponseMapper.ToResponse(vehicle).MotiveVehicleId);

        vehicle.SetMotiveVehicleId("4821");

        Assert.Equal("4821", VehicleResponseMapper.ToResponse(vehicle).MotiveVehicleId);
    }

    [Fact]
    public void The_link_event_stays_internal_to_fleet()
    {
        var vehicle = NewVehicle();
        vehicle.SetMotiveVehicleId("4821");

        Assert.Null(new FleetIntegrationEventMapper().Map(Assert.Single(vehicle.DomainEvents), vehicle));
    }
}
