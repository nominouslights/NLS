using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Maintenance;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// The PM query shapes against real Postgres — this is what guards the translation-risk
/// class of bug (jsonb_array_length counts, the folded-completion fetch, the disposed-vehicle
/// anti-joins) that no in-memory provider can prove. Each test that runs a fleet- or
/// tenant-wide query uses its own tenant id, because every test in the collection shares one
/// database and the fleet dashboard reads every vehicle the tenant owns.
/// </summary>
[Collection("postgres")]
public class PmReadServiceTests(PostgresFixture fixture)
{
    private static readonly DateOnly Today = PmSchedule.TodayUtc();

    private async Task SaveAsync(Guid tenant, Action<FleetDbContext> populate)
    {
        await using var context = fixture.CreateContext(tenant);
        populate(context);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Unassigned_vehicle_reports_assigned_false_not_a_404()
    {
        var tenant = Guid.NewGuid();
        var vehicle = TestVehicleFactory.Create(tenant);
        await SaveAsync(tenant, c => c.Vehicles.Add(vehicle));
        await fixture.RebuildFleetProjectionsAsync();

        await using var reader = fixture.CreateContext(tenant);
        var service = new PmReadService(reader);

        var status = await service.GetVehicleStatusAsync(vehicle.Id);
        Assert.NotNull(status);
        Assert.False(status.Assigned);
        Assert.Null(status.PlanId);
        Assert.Empty(status.Entries);

        var due = await service.GetDueAsync(vehicle.Id);
        Assert.NotNull(due);
        Assert.False(due.Assigned);
        Assert.Equal(0, due.TotalShopMinutes);
    }

    [Fact]
    public async Task Vehicle_status_runs_the_full_260_line_plan_through_not_yet_recorded_overdue_and_ok()
    {
        var tenant = Guid.NewGuid();
        var vehicle = TestVehicleFactory.Create(tenant, odometerKm: 150_000, endOfLifeKm: 1_000_000);
        var plan = PmTestData.SeedDataPlan(tenant, PmTestData.UniqueName("Status plan"));
        await SaveAsync(tenant, c =>
        {
            c.Vehicles.Add(vehicle);
            c.MaintenancePlans.Add(plan);
            c.PlanAssignments.Add(PmTestData.Assign(tenant, vehicle.Id, plan.Id));
        });
        await fixture.RebuildFleetProjectionsAsync();

        // 1. Assigned, nothing ever logged: every line of the plan — 250 items + 10
        //    overhauls — is NotYetRecorded, never silently "compliant".
        await using (var reader = fixture.CreateContext(tenant))
        {
            var status = await new PmReadService(reader).GetVehicleStatusAsync(vehicle.Id);
            Assert.NotNull(status);
            Assert.True(status.Assigned);
            Assert.Equal(plan.Id, status.PlanId);
            Assert.Equal(150_000, status.CurrentOdometerKm);
            Assert.Equal(260, status.Entries.Count);
            Assert.All(status.Entries, e => Assert.Equal(nameof(PmDueState.NotYetRecorded), e.State));
        }

        // 2. PM-E-001 (10,000 km / 6 months) done 7 months ago at 12,000 km back: both
        //    interval arms have lapsed — Overdue, with the next-due values computed from the
        //    completion, not the vehicle.
        var sevenMonthsAgo = Today.AddMonths(-7);
        await SaveAsync(tenant, c => c.PmCompletions.Add(PmTestData.Completion(
            tenant, vehicle.Id, plan.Id, "PM-E-001", PmEntryKind.Item, sevenMonthsAgo, 138_000)));
        await fixture.RebuildFleetProjectionsAsync();

        await using (var reader = fixture.CreateContext(tenant))
        {
            var status = await new PmReadService(reader).GetVehicleStatusAsync(vehicle.Id);
            var entry = Assert.Single(status!.Entries, e => e.Code == "PM-E-001");
            Assert.Equal(nameof(PmDueState.Overdue), entry.State);
            Assert.Equal(138_000, entry.LastDoneKm);
            Assert.Equal(sevenMonthsAgo, entry.LastDoneDate);
            Assert.Equal(148_000, entry.NextDueKm);
            Assert.Equal(sevenMonthsAgo.AddMonths(6), entry.NextDueDate);
            Assert.Equal(-2_000, entry.KmRemaining);
            Assert.True(entry.DaysRemaining <= 0);
        }

        // 3. Logged again today at the current odometer: the fold picks the newer
        //    completion and the line goes back to Ok.
        await SaveAsync(tenant, c => c.PmCompletions.Add(PmTestData.Completion(
            tenant, vehicle.Id, plan.Id, "PM-E-001", PmEntryKind.Item, Today, 150_000)));
        await fixture.RebuildFleetProjectionsAsync();

        await using (var okReader = fixture.CreateContext(tenant))
        {
            var status = await new PmReadService(okReader).GetVehicleStatusAsync(vehicle.Id);
            var entry = Assert.Single(status!.Entries, e => e.Code == "PM-E-001");
            Assert.Equal(nameof(PmDueState.Ok), entry.State);
            Assert.Equal(150_000, entry.LastDoneKm);
            Assert.Equal(160_000, entry.NextDueKm);
        }
    }

    [Fact]
    public async Task Due_view_sums_shop_minutes_groups_by_system_and_flags_never_recorded()
    {
        var tenant = Guid.NewGuid();
        var vehicle = TestVehicleFactory.Create(tenant, odometerKm: 100_000, endOfLifeKm: 1_000_000);
        var plan = PmTestData.Plan(
            tenant,
            PmTestData.UniqueName("Due plan"),
            [
                PmTestData.Item("PM-EN-001", "Engine", "Engine oil & filter", 10_000, null, 30),
                PmTestData.Item("PM-EN-002", "Engine", "Air filter", 5_000, null, 20),
                PmTestData.Item("PM-BR-001", "Brakes", "Brake fluid", null, 6, 15),
            ],
            [PmTestData.Overhaul("OH-X1", "Engine overhaul", 100_000, null, 2m, 1_000m)]);
        var recently = Today.AddDays(-10);
        await SaveAsync(tenant, c =>
        {
            c.Vehicles.Add(vehicle);
            c.MaintenancePlans.Add(plan);
            c.PlanAssignments.Add(PmTestData.Assign(tenant, vehicle.Id, plan.Id));
            // 11,000 km ago on a 10,000 km interval → Overdue (km remaining -1,000).
            c.PmCompletions.Add(PmTestData.Completion(
                tenant, vehicle.Id, plan.Id, "PM-EN-001", PmEntryKind.Item, recently, 89_000));
            // 4,000 km ago on a 5,000 km interval → DueSoon (1,000 km left, default 2,000 lead).
            c.PmCompletions.Add(PmTestData.Completion(
                tenant, vehicle.Id, plan.Id, "PM-EN-002", PmEntryKind.Item, recently, 96_000));
            // 10,000 km ago on a 100,000 km interval → Ok, must NOT count toward the total.
            c.PmCompletions.Add(PmTestData.Completion(
                tenant, vehicle.Id, plan.Id, "OH-X1", PmEntryKind.Overhaul, recently, 90_000));
            // PM-BR-001 never logged → NotYetRecorded, listed separately, not "due".
        });
        await fixture.RebuildFleetProjectionsAsync();

        await using var reader = fixture.CreateContext(tenant);
        var due = await new PmReadService(reader).GetDueAsync(vehicle.Id);

        Assert.NotNull(due);
        Assert.True(due.Assigned);
        Assert.Equal(50, due.TotalShopMinutes); // 30 (Overdue) + 20 (DueSoon), nothing else

        var group = Assert.Single(due.Groups);
        Assert.Equal("Engine", group.System);
        Assert.Equal(new[] { "PM-EN-001", "PM-EN-002" }, group.Entries.Select(e => e.Code));
        Assert.Equal(
            new[] { nameof(PmDueState.Overdue), nameof(PmDueState.DueSoon) },
            group.Entries.Select(e => e.State));

        var notYetRecorded = Assert.Single(due.NotYetRecorded);
        Assert.Equal("PM-BR-001", notYetRecorded.Code);
    }

    [Fact]
    public async Task Overhauls_view_carries_the_latest_measurement_of_each_related_test_item()
    {
        var tenant = Guid.NewGuid();
        var vehicle = TestVehicleFactory.Create(tenant, odometerKm: 100_000, endOfLifeKm: 1_000_000);
        var plan = PmTestData.Plan(
            tenant,
            PmTestData.UniqueName("Overhaul plan"),
            [PmTestData.Item("PM-T-001", "Engine", "Compression test", 40_000, null, 90, task: MaintenanceTask.Test)],
            [PmTestData.Overhaul("OH-T1", "Engine (3.7L Ti-VCT V6)", 320_000, 180, 40m, 6_500m, "PM-T-001")]);
        await SaveAsync(tenant, c =>
        {
            c.Vehicles.Add(vehicle);
            c.MaintenancePlans.Add(plan);
            c.PlanAssignments.Add(PmTestData.Assign(tenant, vehicle.Id, plan.Id));
            c.PmCompletions.Add(PmTestData.Completion(
                tenant, vehicle.Id, plan.Id, "PM-T-001", PmEntryKind.Item,
                Today.AddDays(-100), 90_000, measurement: "150 psi all cylinders"));
            c.PmCompletions.Add(PmTestData.Completion(
                tenant, vehicle.Id, plan.Id, "PM-T-001", PmEntryKind.Item,
                Today.AddDays(-10), 98_000, measurement: "140 psi all cylinders"));
        });
        await fixture.RebuildFleetProjectionsAsync();

        await using var reader = fixture.CreateContext(tenant);
        var overhauls = await new PmReadService(reader).GetOverhaulsAsync(vehicle.Id);

        Assert.NotNull(overhauls);
        Assert.True(overhauls.Assigned);
        var overhaul = Assert.Single(overhauls.Overhauls);
        Assert.Equal("OH-T1", overhaul.Code);
        Assert.Equal(nameof(PmDueState.NotYetRecorded), overhaul.State); // the overhaul itself was never logged

        var related = Assert.Single(overhaul.RelatedMeasurements);
        Assert.Equal("PM-T-001", related.ItemCode);
        Assert.Equal("Compression test", related.Component);
        Assert.Equal("140 psi all cylinders", related.Measurement); // latest, not first
        Assert.Equal(Today.AddDays(-10), related.PerformedAt);
        Assert.Equal(98_000, related.OdometerKm);
    }

    [Fact]
    public async Task History_is_newest_first_respects_the_limit_and_404s_an_unknown_vehicle()
    {
        var tenant = Guid.NewGuid();
        var vehicle = TestVehicleFactory.Create(tenant, odometerKm: 100_000, endOfLifeKm: 1_000_000);
        var planId = Guid.NewGuid(); // history reads rows only — no plan needed
        await SaveAsync(tenant, c =>
        {
            c.Vehicles.Add(vehicle);
            c.PmCompletions.Add(PmTestData.Completion(
                tenant, vehicle.Id, planId, "PM-H-001", PmEntryKind.Item, Today.AddDays(-3), 97_000));
            c.PmCompletions.Add(PmTestData.Completion(
                tenant, vehicle.Id, planId, "PM-H-002", PmEntryKind.Item, Today.AddDays(-2), 98_000));
            c.PmCompletions.Add(PmTestData.Completion(
                tenant, vehicle.Id, planId, "PM-H-003", PmEntryKind.Item, Today.AddDays(-1), 99_000));
        });
        await fixture.RebuildFleetProjectionsAsync();

        await using var reader = fixture.CreateContext(tenant);
        var service = new PmReadService(reader);

        var history = await service.GetHistoryAsync(vehicle.Id, limit: 2);
        Assert.NotNull(history);
        Assert.Equal(new[] { "PM-H-003", "PM-H-002" }, history.Select(c => c.Code));

        Assert.Null(await service.GetHistoryAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Fleet_due_orders_by_urgency_counts_never_serviced_lines_and_excludes_disposed_vehicles()
    {
        var tenant = Guid.NewGuid();
        var seedPlan = PmTestData.SeedDataPlan(tenant, PmTestData.UniqueName("Fleet big plan"));
        var smallPlan = PmTestData.Plan(
            tenant,
            PmTestData.UniqueName("Fleet small plan"),
            [PmTestData.Item("PM-S-001", "Engine", "Engine oil & filter", 10_000, null, 30)],
            []);

        var freshVehicle = TestVehicleFactory.Create(tenant, odometerKm: 100_000, endOfLifeKm: 1_000_000);
        var overdueVehicle = TestVehicleFactory.Create(tenant, odometerKm: 100_000, endOfLifeKm: 1_000_000);
        var disposedVehicle = TestVehicleFactory.Create(tenant, odometerKm: 100_000, endOfLifeKm: 1_000_000);
        var unassignedVehicle = TestVehicleFactory.Create(tenant, odometerKm: 100_000, endOfLifeKm: 1_000_000);

        Assert.True(disposedVehicle.ChangeStatus(VehicleStatus.Retired).IsSuccess);
        Assert.True(disposedVehicle.Dispose(DisposalMethod.Sold, salePriceCad: 5_000m).IsSuccess);

        await SaveAsync(tenant, c =>
        {
            c.Vehicles.AddRange(freshVehicle, overdueVehicle, disposedVehicle, unassignedVehicle);
            c.MaintenancePlans.AddRange(seedPlan, smallPlan);
            c.PlanAssignments.Add(PmTestData.Assign(tenant, freshVehicle.Id, seedPlan.Id));
            c.PlanAssignments.Add(PmTestData.Assign(tenant, overdueVehicle.Id, smallPlan.Id));
            c.PlanAssignments.Add(PmTestData.Assign(tenant, disposedVehicle.Id, smallPlan.Id));
            // 12,000 km ago on a 10,000 km interval → one Overdue line for overdueVehicle.
            c.PmCompletions.Add(PmTestData.Completion(
                tenant, overdueVehicle.Id, smallPlan.Id, "PM-S-001", PmEntryKind.Item,
                Today.AddDays(-30), 88_000));
        });
        await fixture.RebuildFleetProjectionsAsync();

        await using var reader = fixture.CreateContext(tenant);
        var fleet = await new PmReadService(reader).GetFleetDueAsync();

        // Disposed and unassigned vehicles never appear; most urgent first (an overdue line
        // outranks any number of never-recorded ones).
        Assert.Equal(new[] { overdueVehicle.Id, freshVehicle.Id }, fleet.Vehicles.Select(v => v.VehicleId));

        var overdueRow = fleet.Vehicles[0];
        Assert.Equal(1, overdueRow.OverdueCount);
        Assert.Equal("PM-S-001", Assert.Single(overdueRow.DueEntries).Code);

        var freshRow = fleet.Vehicles[1];
        Assert.Equal(0, freshRow.OverdueCount);
        Assert.Equal(0, freshRow.DueSoonCount);
        Assert.Equal(260, freshRow.NotYetRecordedCount); // never-serviced ≠ compliant
        Assert.Empty(freshRow.DueEntries);
    }

    [Fact]
    public async Task Plan_list_counts_lines_server_side_and_excludes_disposed_vehicles_from_assigned_counts()
    {
        var tenant = Guid.NewGuid();
        var plan = PmTestData.Plan(
            tenant,
            PmTestData.UniqueName("List plan"),
            [
                PmTestData.Item("PM-L-001", "Engine", "Engine oil & filter", 10_000, 6, 30),
                PmTestData.Item("PM-L-002", "Brakes", "Brake fluid", null, 12, 15),
            ],
            [PmTestData.Overhaul("OH-L1", "Engine overhaul", 320_000, 180, 40m, 6_500m)]);

        var liveVehicle = TestVehicleFactory.Create(tenant, odometerKm: 100_000, endOfLifeKm: 1_000_000);
        var disposedVehicle = TestVehicleFactory.Create(tenant, odometerKm: 100_000, endOfLifeKm: 1_000_000);
        Assert.True(disposedVehicle.ChangeStatus(VehicleStatus.Retired).IsSuccess);
        Assert.True(disposedVehicle.Dispose(DisposalMethod.Recycled).IsSuccess);

        await SaveAsync(tenant, c =>
        {
            c.Vehicles.AddRange(liveVehicle, disposedVehicle);
            c.MaintenancePlans.Add(plan);
            c.PlanAssignments.Add(PmTestData.Assign(tenant, liveVehicle.Id, plan.Id));
            c.PlanAssignments.Add(PmTestData.Assign(tenant, disposedVehicle.Id, plan.Id));
        });
        await fixture.RebuildFleetProjectionsAsync();

        await using var reader = fixture.CreateContext(tenant);
        var plans = await new PmReadService(reader).GetPlansAsync();

        // The tenant is this test's own, so exactly one plan is visible — and its counts
        // come from server-side jsonb_array_length, the translation this test pins.
        var summary = Assert.Single(plans);
        Assert.Equal(plan.Id, summary.Id);
        Assert.Equal(2, summary.ItemCount);
        Assert.Equal(1, summary.OverhaulCount);
        Assert.Equal(1, summary.AssignedVehicleCount); // the disposed unit no longer counts
    }
}
