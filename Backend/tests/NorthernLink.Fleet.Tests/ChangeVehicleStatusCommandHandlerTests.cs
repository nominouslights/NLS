using NorthernLink.Fleet.Application.Vehicles.ChangeStatus;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class ChangeVehicleStatusCommandHandlerTests
{
    [Fact]
    public async Task Unknown_vehicle_is_not_found()
    {
        var repository = new InMemoryVehicleRepository();
        var handler = new ChangeVehicleStatusCommandHandler(repository);

        var result = await handler.Handle(
            new ChangeVehicleStatusCommand(TestVehicles.TenantId, Guid.NewGuid(), VehicleStatus.InMaintenance, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.NotFound, result.Error);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task Out_of_service_without_reason_is_rejected()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create();
        repository.Add(vehicle);
        var handler = new ChangeVehicleStatusCommandHandler(repository);

        var result = await handler.Handle(
            new ChangeVehicleStatusCommand(TestVehicles.TenantId, vehicle.Id, VehicleStatus.OutOfService, "  "),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(VehicleErrors.StatusReasonRequired, result.Error);
        Assert.Equal(VehicleStatus.Active, vehicle.Status);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Retiring_a_vehicle_issues_a_certificate_in_the_same_unit_of_work()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create(odometerKm: 240_000);
        repository.Add(vehicle);
        var handler = new ChangeVehicleStatusCommandHandler(repository);

        var result = await handler.Handle(
            new ChangeVehicleStatusCommand(TestVehicles.TenantId, vehicle.Id, VehicleStatus.Retired, "Fleet renewal"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleStatus.Retired, vehicle.Status);

        var certificate = Assert.Single(repository.Certificates);
        Assert.Equal(vehicle.Id, certificate.VehicleId);
        Assert.Equal(TestVehicles.TenantId, certificate.TenantId);
        Assert.Equal($"RC-{DateTimeOffset.UtcNow.Year}-0001", certificate.CertificateNumber);
        Assert.Equal(vehicle.Vin.Value, certificate.Vin);
        Assert.Equal(vehicle.UnitNumber, certificate.UnitNumber);
        Assert.Equal(240_000, certificate.FinalOdometerKm);
        Assert.Equal("Fleet renewal", certificate.RetirementReason);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Certificate_sequence_increments_per_tenant_and_year()
    {
        var repository = new InMemoryVehicleRepository();
        var first = TestVehicles.Create(unitNumber: "U-01", vin: "1HVBBAAN2RH554471");
        var second = TestVehicles.Create(unitNumber: "U-02", vin: "1HVBBAAN6RH555522");
        repository.Add(first);
        repository.Add(second);
        var handler = new ChangeVehicleStatusCommandHandler(repository);

        _ = await handler.Handle(
            new ChangeVehicleStatusCommand(TestVehicles.TenantId, first.Id, VehicleStatus.Retired, null),
            CancellationToken.None);
        _ = await handler.Handle(
            new ChangeVehicleStatusCommand(TestVehicles.TenantId, second.Id, VehicleStatus.Retired, null),
            CancellationToken.None);

        Assert.Equal(2, repository.Certificates.Count);
        Assert.EndsWith("-0001", repository.Certificates[0].CertificateNumber);
        Assert.EndsWith("-0002", repository.Certificates[1].CertificateNumber);
    }

    [Fact]
    public async Task Illegal_transition_is_a_conflict_and_issues_no_certificate()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.InStatus(VehicleStatus.Retired);
        repository.Add(vehicle);
        var handler = new ChangeVehicleStatusCommandHandler(repository);

        var result = await handler.Handle(
            new ChangeVehicleStatusCommand(TestVehicles.TenantId, vehicle.Id, VehicleStatus.Active, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Fleet.Vehicle.InvalidStatusTransition", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Empty(repository.Certificates);
    }

    [Fact]
    public async Task Re_retiring_an_already_retired_vehicle_never_double_issues()
    {
        var repository = new InMemoryVehicleRepository();
        var vehicle = TestVehicles.Create();
        repository.Add(vehicle);
        var handler = new ChangeVehicleStatusCommandHandler(repository);

        _ = await handler.Handle(
            new ChangeVehicleStatusCommand(TestVehicles.TenantId, vehicle.Id, VehicleStatus.Retired, null),
            CancellationToken.None);
        var second = await handler.Handle(
            new ChangeVehicleStatusCommand(TestVehicles.TenantId, vehicle.Id, VehicleStatus.Retired, null),
            CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Single(repository.Certificates);
    }
}
