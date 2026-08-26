using Microsoft.EntityFrameworkCore;
using Npgsql;
using NorthernLink.Fleet.Application.Maintenance.Assignments.Unassign;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// Drives the real projection worker over the three PM read models against real Postgres:
/// the full seeded plan document (250 items + 10 overhauls) survives the jsonb round-trip
/// into rm_maintenance_plans with enums as strings and absent leads as nulls; assignment and
/// completion rows mirror their aggregates; and an unassignment (hard delete, journalled via
/// the synthetic aggregate-deleted row) removes the rm assignment row on the next poll.
/// </summary>
[Collection("postgres")]
public class PmProjectionTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Seed_sized_plan_assignment_and_completion_project_into_the_read_models()
    {
        var tenant = PostgresFixture.TenantA;

        var vehicle = TestVehicleFactory.Create(tenant, odometerKm: 100_000, endOfLifeKm: 500_000);
        var plan = PmTestData.SeedDataPlan(tenant, PmTestData.UniqueName("Projection plan"));
        var assignment = PmTestData.Assign(tenant, vehicle.Id, plan.Id);
        // Odometer below the vehicle's current reading, so the wired odometer-propagation
        // reaction is a deliberate no-op here (it has its own flow test).
        var completion = PmTestData.Completion(
            tenant, vehicle.Id, plan.Id, "PM-E-001", PmEntryKind.Item, PmSchedule.TodayUtc(), 99_000);

        await using (var writer = fixture.CreateContext(tenant))
        {
            writer.Vehicles.Add(vehicle);
            writer.MaintenancePlans.Add(plan);
            writer.PlanAssignments.Add(assignment);
            writer.PmCompletions.Add(completion);
            await writer.SaveChangesAsync();
        }

        // Drain rather than a single poll: any backlog left by earlier classes could push this
        // test's rows past one poll's BatchSize window.
        await fixture.DrainFleetProjectionsAsync();

        await using (var reader = fixture.CreateContext(tenant))
        {
            // The 260-line document round-trips through EF's owned-JSON mapping intact.
            var planRow = await reader.MaintenancePlanReadModels.SingleAsync(p => p.Id == plan.Id);
            Assert.Equal(250, planRow.Items.Count);
            Assert.Equal(10, planRow.Overhauls.Count);

            var oilChange = planRow.Items[0];
            Assert.Equal("PM-E-001", oilChange.Code);
            Assert.Equal(ComponentTier.Primary, oilChange.Tier);
            Assert.Equal(MaintenanceTask.Replace, oilChange.Task);
            Assert.Null(oilChange.LeadKm);
            Assert.Null(oilChange.LeadDays);

            var engineOverhaul = planRow.Overhauls.Single(o => o.Code == "OH-01");
            Assert.Equal(40m, engineOverhaul.LabourHours);
            Assert.Equal(
                new[] { "PM-E-026", "PM-E-027", "PM-E-028", "PM-E-029" },
                engineOverhaul.RelatedItemCodes);

            var assignmentRow = await reader.PlanAssignmentReadModels.SingleAsync(a => a.Id == assignment.Id);
            Assert.Equal(vehicle.Id, assignmentRow.VehicleId);
            Assert.Equal(plan.Id, assignmentRow.PlanId);
            // Postgres timestamptz keeps microseconds, DateTimeOffset ticks are 100ns — compare
            // at the precision the round-trip preserves.
            Assert.Equal(assignment.AssignedAtUtc, assignmentRow.AssignedAtUtc, TimeSpan.FromMilliseconds(1));

            var completionRow = await reader.PmCompletionReadModels.SingleAsync(c => c.Id == completion.Id);
            Assert.Equal(vehicle.Id, completionRow.VehicleId);
            Assert.Equal(plan.Id, completionRow.PlanId);
            Assert.Equal("PM-E-001", completionRow.ItemCode);
            Assert.Equal(nameof(PmEntryKind.Item), completionRow.Kind);
            Assert.Equal(99_000, completionRow.OdometerKm);
        }

        // The stored jsonb itself: enums are strings ("Primary"/"Replace"), never opaque
        // integers, and the unset lead overrides are nulls — asserted with raw SQL because
        // an EF round-trip alone cannot distinguish "string in the database" from
        // "int the mapper happened to convert back".
        await using var connection = await fixture.OpenRawConnectionAsync(tenant);
        await using var command = new NpgsqlCommand(
            """
            SELECT jsonb_array_length(items),
                   jsonb_array_length(overhauls),
                   items -> 0 ->> 'Code',
                   items -> 0 ->> 'Tier',
                   items -> 0 ->> 'Task',
                   items -> 0 ->> 'LeadKm'
            FROM fleet.rm_maintenance_plans
            WHERE id = @id
            """, connection);
        command.Parameters.AddWithValue("id", plan.Id);
        await using var row = await command.ExecuteReaderAsync();
        Assert.True(await row.ReadAsync());
        Assert.Equal(250, row.GetInt32(0));
        Assert.Equal(10, row.GetInt32(1));
        Assert.Equal("PM-E-001", row.GetString(2));
        Assert.Equal("Primary", row.GetString(3));
        Assert.Equal("Replace", row.GetString(4));
        Assert.True(row.IsDBNull(5));
    }

    [Fact]
    public async Task Unassigning_removes_the_rm_assignment_row_on_the_next_poll()
    {
        var tenant = PostgresFixture.TenantA;

        var vehicle = TestVehicleFactory.Create(tenant);
        var plan = PmTestData.Plan(
            tenant,
            PmTestData.UniqueName("Unassign plan"),
            [PmTestData.Item("PM-X-001", "Engine", "Engine oil & filter", 10_000, 6, 30)],
            []);
        var assignment = PmTestData.Assign(tenant, vehicle.Id, plan.Id);

        await using (var writer = fixture.CreateContext(tenant))
        {
            writer.Vehicles.Add(vehicle);
            writer.MaintenancePlans.Add(plan);
            writer.PlanAssignments.Add(assignment);
            await writer.SaveChangesAsync();
        }

        await fixture.DrainFleetProjectionsAsync();

        await using (var reader = fixture.CreateContext(tenant))
        {
            Assert.True(await reader.PlanAssignmentReadModels.AnyAsync(a => a.Id == assignment.Id));
        }

        // The real unassign path: MarkRemoved + hard delete, journalled via the synthetic
        // aggregate-deleted row that drives the projection's read-row removal.
        await using (var writer = fixture.CreateContext(tenant))
        {
            var handler = new UnassignMaintenancePlanCommandHandler(new PlanAssignmentRepository(writer));
            var result = await handler.Handle(
                new UnassignMaintenancePlanCommand(tenant, vehicle.Id), CancellationToken.None);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        }

        await fixture.DrainFleetProjectionsAsync();

        await using (var reader = fixture.CreateContext(tenant))
        {
            Assert.False(await reader.PlanAssignmentReadModels.AnyAsync(a => a.Id == assignment.Id));
        }
    }
}
