using Npgsql;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.WorkOrders;
using NorthernLink.Fleet.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// The work order <c>defects</c> jsonb against a real Postgres as the non-superuser app role:
/// the lines round-trip through the aggregate table AND the projection into rm_work_orders with
/// their enums as names, and the new column is covered by the existing tenant-isolation policy on
/// both tables. A correct domain with a one-sided enum conversion would pass every unit test and
/// fail only here.
/// </summary>
[Collection("postgres")]
public class WorkOrderDefectLinesPersistenceTests(PostgresFixture fixture)
{
    private static WorkOrder WorkOrderWithLines(Guid tenantId, Guid inspectionId) =>
        WorkOrder.Create(
            tenantId,
            Guid.NewGuid(),
            number: $"WO-{Guid.NewGuid().ToString("N")[..8]}",
            title: "Repair DVIR defects",
            description: null,
            WorkOrderPriority.High,
            WorkOrderSource.PreTripInspection,
            sourceRef: null,
            createdBy: "Dispatch",
            assignedTo: null,
            dueDate: null,
            lineItems: [],
            shopId: null,
            authorizedLimitCad: null,
            budgetCode: null,
            dateRequiredOrOos: null,
            defects: [
                new WorkOrderDefectLine
                {
                    InspectionId = inspectionId, Item = "Brakes", Severity = InspectionDefectSeverity.OutOfService, Note = "grinding",
                },
                new WorkOrderDefectLine
                {
                    InspectionId = inspectionId, Item = "Wipers", Severity = InspectionDefectSeverity.Minor,
                },
            ]).Value;

    private static async Task<string?> ScalarAsync(NpgsqlConnection connection, string sql, Guid id)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        return (await command.ExecuteScalarAsync())?.ToString();
    }

    [Fact]
    public async Task Lines_and_outcomes_round_trip_through_the_aggregate_and_the_projection()
    {
        var inspectionId = Guid.NewGuid();
        var workOrder = WorkOrderWithLines(PostgresFixture.TenantA, inspectionId);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.WorkOrders.Add(workOrder);
            await writer.SaveChangesAsync();
        }

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var loaded = await new WorkOrderRepository(writer).GetByIdAsync(workOrder.Id);
            Assert.NotNull(loaded);
            Assert.Equal(2, loaded.Defects.Count);
            Assert.Equal(InspectionDefectSeverity.OutOfService, loaded.Defects[0].Severity);

            Assert.True(loaded.Complete(Guid.NewGuid(), [
                new WorkOrderDefectOutcome(inspectionId, "Brakes", DefectRepairOutcome.Repaired, "new pads"),
                new WorkOrderDefectOutcome(inspectionId, "Wipers", DefectRepairOutcome.Deferred, "blade on order"),
            ]).IsSuccess);
            await writer.SaveChangesAsync();
        }

        // The jsonb stores names, never ints — it is read by people and hand-written SQL.
        await using (var connection = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantA))
        {
            var raw = await ScalarAsync(connection, "SELECT defects::text FROM fleet.work_orders WHERE id = @id", workOrder.Id);
            Assert.Contains("\"OutOfService\"", raw);
            Assert.Contains("\"Repaired\"", raw);
            Assert.Contains("\"Deferred\"", raw);
        }

        await fixture.RebuildFleetProjectionsAsync();

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var response = (await new WorkOrderReadService(reader).GetAllAsync()).Single(w => w.Id == workOrder.Id);

        Assert.Equal(2, response.Defects.Count);
        var brakes = response.Defects.Single(d => d.Item == "Brakes");
        Assert.Equal(inspectionId, brakes.InspectionId);
        Assert.Equal("OutOfService", brakes.Severity);
        Assert.Equal("grinding", brakes.Note);
        Assert.Equal("Repaired", brakes.Outcome);
        Assert.Equal("new pads", brakes.OutcomeNote);

        var wipers = response.Defects.Single(d => d.Item == "Wipers");
        Assert.Equal("Minor", wipers.Severity);
        Assert.Equal("Deferred", wipers.Outcome);
        Assert.Equal("blade on order", wipers.OutcomeNote);
    }

    [Fact]
    public async Task A_work_order_without_lines_stores_an_empty_array_and_reads_back_empty()
    {
        var workOrder = WorkOrder.Create(
            PostgresFixture.TenantA, Guid.NewGuid(), $"WO-{Guid.NewGuid().ToString("N")[..8]}", "Manual job", null,
            WorkOrderPriority.Low, WorkOrderSource.Manual, null, "Dispatch", null, null,
            ["Inspect"], null, null, null, null).Value;

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.WorkOrders.Add(workOrder);
            await writer.SaveChangesAsync();
        }

        await using (var connection = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantA))
        {
            Assert.Equal("[]", await ScalarAsync(connection, "SELECT defects::text FROM fleet.work_orders WHERE id = @id", workOrder.Id));
        }

        await fixture.RebuildFleetProjectionsAsync();

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        var response = (await new WorkOrderReadService(reader).GetAllAsync()).Single(w => w.Id == workOrder.Id);
        Assert.Empty(response.Defects);
    }

    [Fact]
    public async Task Defect_lines_are_invisible_to_other_tenants_on_both_tables()
    {
        var workOrder = WorkOrderWithLines(PostgresFixture.TenantA, Guid.NewGuid());

        await using (var context = fixture.CreateContext(PostgresFixture.TenantA, withMapper: true))
        {
            context.WorkOrders.Add(workOrder);
            await context.SaveChangesAsync();
        }

        await fixture.RebuildFleetProjectionsAsync();

        const string writeSide = "SELECT count(*) FROM fleet.work_orders WHERE id = @id AND jsonb_array_length(defects) = 2";
        const string readSide = "SELECT count(*) FROM fleet.rm_work_orders WHERE id = @id AND jsonb_array_length(defects) = 2";

        await using (var tenantA = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantA))
        {
            Assert.Equal("1", await ScalarAsync(tenantA, writeSide, workOrder.Id));
            Assert.Equal("1", await ScalarAsync(tenantA, readSide, workOrder.Id));
        }

        await using var tenantB = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantB);
        Assert.Equal("0", await ScalarAsync(tenantB, writeSide, workOrder.Id));
        Assert.Equal("0", await ScalarAsync(tenantB, readSide, workOrder.Id));
    }
}
