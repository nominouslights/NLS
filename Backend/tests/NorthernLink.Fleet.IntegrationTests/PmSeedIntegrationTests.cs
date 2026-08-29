using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Maintenance.Plans.SeedDefaults;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// The idempotent default-plan seed against real Postgres — the unique (tenant_id, name)
/// index and the TryAdd race-closers only exist in real SQL. Each test uses its own tenant
/// because the seed's plan name is fixed and tenant-unique.
/// </summary>
[Collection("postgres")]
public class PmSeedIntegrationTests(PostgresFixture fixture)
{
    private static Vehicle SeedVehicle(Guid tenantId) =>
        Vehicle.Register(
            tenantId,
            TransitSeverePlanSeed.UnitNumber,
            Vin.Create(TransitSeverePlanSeed.Vin).Value,
            make: "Ford",
            model: "Transit T-150",
            year: 2016,
            seatingCapacity: 8,
            licencePlate: "MB · NL 001",
            requiredLicenceClass: "Class 5",
            odometerKm: 150_000,
            acquisitionCostCad: 30_000m,
            endOfLifeKm: 500_000).Value;

    private async Task<Guid> RunSeedAsync(Guid tenantId)
    {
        await using var context = fixture.CreateContext(tenantId);
        var handler = new SeedDefaultMaintenancePlanCommandHandler(
            new MaintenancePlanRepository(context),
            new PlanAssignmentRepository(context),
            new VehicleRepository(context));

        var result = await handler.Handle(
            new SeedDefaultMaintenancePlanCommand(tenantId), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    [Fact]
    public async Task Seeding_twice_yields_one_plan_with_the_same_id_and_one_assignment_for_the_seed_vehicle()
    {
        var tenant = Guid.NewGuid();
        var vehicle = SeedVehicle(tenant);
        await using (var context = fixture.CreateContext(tenant))
        {
            context.Vehicles.Add(vehicle);
            await context.SaveChangesAsync();
        }

        var firstRun = await RunSeedAsync(tenant);
        var secondRun = await RunSeedAsync(tenant);

        Assert.Equal(firstRun, secondRun);

        await using var reader = fixture.CreateContext(tenant);
        var plan = await reader.MaintenancePlans
            .SingleAsync(p => p.Name == TransitSeverePlanSeed.PlanName);
        Assert.Equal(firstRun, plan.Id);
        Assert.Equal(250, plan.Items.Count);
        Assert.Equal(10, plan.Overhauls.Count);

        var assignment = await reader.PlanAssignments
            .SingleAsync(a => a.VehicleId == vehicle.Id);
        Assert.Equal(plan.Id, assignment.PlanId);
    }

    [Fact]
    public async Task A_pre_existing_plan_under_the_seed_name_is_reused_untouched()
    {
        var tenant = Guid.NewGuid();
        var handTuned = PmTestData.Plan(
            tenant,
            TransitSeverePlanSeed.PlanName,
            [PmTestData.Item("PM-OPS-001", "Engine", "Operator-tuned oil change", 8_000, 6, 45)],
            []);
        await using (var context = fixture.CreateContext(tenant))
        {
            context.MaintenancePlans.Add(handTuned);
            await context.SaveChangesAsync();
        }

        var seededId = await RunSeedAsync(tenant);

        Assert.Equal(handTuned.Id, seededId);

        await using var reader = fixture.CreateContext(tenant);
        var plan = await reader.MaintenancePlans
            .SingleAsync(p => p.Name == TransitSeverePlanSeed.PlanName);
        // Reused, never recreated or updated: the operator's single hand-tuned line survives.
        Assert.Equal(handTuned.Id, plan.Id);
        var item = Assert.Single(plan.Items);
        Assert.Equal("PM-OPS-001", item.Code);
        Assert.Empty(plan.Overhauls);
    }
}
