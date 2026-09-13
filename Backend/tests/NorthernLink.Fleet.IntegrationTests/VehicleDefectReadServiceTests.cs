using NorthernLink.Fleet.Application.Inspections.Enter;
using NorthernLink.Fleet.Application.Inspections.Update;
using NorthernLink.Fleet.Application.Services.Add;
using NorthernLink.Fleet.Application.WorkOrders.Complete;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.Services;
using NorthernLink.Fleet.Domain.Vehicles;
using NorthernLink.Fleet.Domain.WorkOrders;
using NorthernLink.Fleet.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// The defect backlog, derived end-to-end against a real Postgres read model as the
/// non-superuser app role. These are the only tests that prove the resolution and recurrence
/// fields actually round-trip: they are new properties on an owned record mapped
/// <c>OwnsMany(...).ToJson("defects")</c>, so a correct domain with a broken jsonb mapping —
/// or a command handler that drops the fields — would pass every unit test and still show
/// repaired defects as open on every dispatcher's screen.
/// </summary>
[Collection("postgres")]
public class VehicleDefectReadServiceTests(PostgresFixture fixture)
{
    private static InspectionDefect Defect(
        string item,
        InspectionDefectSeverity severity = InspectionDefectSeverity.Major,
        string? note = "as found") =>
        new() { Item = item, Severity = severity, Note = note };

    private static VehicleInspection Inspection(
        Guid tenantId,
        Guid? vehicleId,
        string unit,
        IReadOnlyList<InspectionDefect> defects,
        DateTimeOffset? performedAt = null) =>
        VehicleInspection.Enter(
            tenantId,
            InspectionSource.Dispatcher,
            InspectionType.PreTrip,
            // Standalone vehicle entries (no trip number) can repeat freely — the partial unique
            // index only constrains trip-context inspections.
            tripNumber: null,
            vehicleId,
            unit,
            driverName: "J. Spence",
            enteredBy: null,
            performedAt: performedAt ?? DateTimeOffset.UtcNow,
            odometerKm: 118_204,
            checklistItems: [],
            defects,
            weather: [],
            temperatureC: null,
            roadConditions: [],
            visibility: null,
            roadAdvisories: null,
            fuelLevel: null,
            issues: [],
            attestations: [],
            driverSignatureName: null,
            certifiedAt: null,
            fuelAdded: false,
            fuelLitres: null,
            fuelCostCad: null).Value;

    /// <summary>Writes a vehicle for TenantA and returns it, unit number included.</summary>
    private async Task<Vehicle> SeedVehicleAsync()
    {
        var vehicle = TestVehicleFactory.Create(PostgresFixture.TenantA);

        await using var writer = fixture.CreateContext(PostgresFixture.TenantA);
        writer.Vehicles.Add(vehicle);
        await writer.SaveChangesAsync();

        return vehicle;
    }

    private async Task<IReadOnlyList<Application.Inspections.VehicleDefectResponse>> ReadAsync(
        Guid vehicleId, bool includeResolved = false)
    {
        await fixture.RebuildFleetProjectionsAsync();

        await using var reader = fixture.CreateContext(PostgresFixture.TenantA);
        return await new VehicleDefectReadService(reader).GetDefectsForVehicleAsync(vehicleId, includeResolved);
    }

    [Fact]
    public async Task A_defect_with_no_resolution_stamp_reads_back_open()
    {
        // The headline behaviour: everything recorded before this change has no resolution stamp
        // in its jsonb, so the whole existing backlog reads back as unresolved. Zero backfill.
        var vehicle = await SeedVehicleAsync();

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.VehicleInspections.Add(Inspection(
                PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, [Defect("Brakes")]));
            await writer.SaveChangesAsync();
        }

        var defects = await ReadAsync(vehicle.Id);

        var only = Assert.Single(defects);
        Assert.Equal("Brakes", only.Item);
        Assert.Equal("Major", only.Severity);
        Assert.Equal("as found", only.Note);
        Assert.Equal(vehicle.UnitNumber, only.Unit);
        Assert.Equal("PreTrip", only.InspectionType);
        Assert.Equal("J. Spence", only.DriverName);
        Assert.Null(only.ResolutionReason);
        Assert.Null(only.ResolvedAtUtc);
        Assert.Null(only.WorkOrderId);
        Assert.Null(only.Recurrence);
    }

    [Fact]
    public async Task A_legacy_defect_whose_jsonb_predates_the_resolution_fields_reads_back_open()
    {
        // The zero-backfill claim, proven against jsonb that literally does not contain the new
        // keys — not merely against a freshly written row whose keys happen to be null. Every
        // defect recorded before this change looks exactly like this in the database.
        var vehicle = await SeedVehicleAsync();
        Guid inspectionId;

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var inspection = Inspection(
                PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, [Defect("Brakes")]);
            writer.VehicleInspections.Add(inspection);
            await writer.SaveChangesAsync();
            inspectionId = inspection.Id;
        }

        // Rewrite the payload to the pre-change shape: item/severity/note and nothing else.
        await using (var connection = await fixture.OpenRawConnectionAsync(PostgresFixture.TenantA))
        {
            await using var command = new Npgsql.NpgsqlCommand(
                """
                UPDATE fleet.vehicle_inspections
                SET defects = '[{"Item":"Brakes","Severity":"Major","Note":"as found"}]'::jsonb
                WHERE id = @id;
                """, connection);
            command.Parameters.AddWithValue("id", inspectionId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        var defects = await ReadAsync(vehicle.Id);

        var only = Assert.Single(defects);
        Assert.Equal("Brakes", only.Item);
        Assert.Equal("Major", only.Severity);
        Assert.Null(only.ResolutionReason);
        Assert.Null(only.ResolvedAtUtc);
        Assert.Null(only.ResolvedBy);
        Assert.Null(only.ResolvedByWorkOrderId);
    }

    [Fact]
    public async Task A_resolved_defect_is_excluded_by_default_and_included_with_the_history_flag()
    {
        var vehicle = await SeedVehicleAsync();
        var resolvedAt = DateTimeOffset.UtcNow.AddHours(-1);

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var inspection = Inspection(
                PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, [Defect("Brakes"), Defect("Defroster")]);

            Assert.True(inspection
                .ResolveDefect("Brakes", DefectResolutionReason.PreviouslyRepaired, "Fixed at Thompson", "R. Ballantyne", resolvedAt)
                .IsSuccess);

            writer.VehicleInspections.Add(inspection);
            await writer.SaveChangesAsync();
        }

        var open = await ReadAsync(vehicle.Id);
        Assert.Equal("Defroster", Assert.Single(open).Item);

        var all = await ReadAsync(vehicle.Id, includeResolved: true);
        Assert.Equal(2, all.Count);

        // Every resolution field survives the jsonb round trip, the enum as its NAME.
        var brakes = all.Single(d => d.Item == "Brakes");
        Assert.Equal("PreviouslyRepaired", brakes.ResolutionReason);
        Assert.Equal("Fixed at Thompson", brakes.ResolutionNote);
        Assert.Equal("R. Ballantyne", brakes.ResolvedBy);
        Assert.Equal(resolvedAt, brakes.ResolvedAtUtc!.Value, TimeSpan.FromMilliseconds(1));
        Assert.Null(brakes.ResolvedByWorkOrderId);
    }

    [Fact]
    public async Task A_legacy_unit_only_inspection_is_matched_by_unit_number()
    {
        // Inspections predating the vehicle link carry VehicleId null. They still describe this
        // truck, and an out-of-service defect on one must not be invisible.
        var vehicle = await SeedVehicleAsync();

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.VehicleInspections.Add(Inspection(
                PostgresFixture.TenantA, vehicleId: null, vehicle.UnitNumber, [Defect("Headlights")]));
            await writer.SaveChangesAsync();
        }

        var defects = await ReadAsync(vehicle.Id);

        var only = Assert.Single(defects);
        Assert.Equal("Headlights", only.Item);
        Assert.Null(only.VehicleId);
        Assert.Equal(vehicle.UnitNumber, only.Unit);
    }

    [Fact]
    public async Task An_unknown_vehicle_id_returns_an_empty_list_rather_than_failing()
    {
        var defects = await ReadAsync(Guid.NewGuid());

        Assert.Empty(defects);
    }

    [Fact]
    public async Task Completing_the_generated_work_order_resolves_the_defects_it_was_raised_for()
    {
        var vehicle = await SeedVehicleAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        Guid inspectionId;
        Guid workOrderId;

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            // Number is varchar(16) and unique per tenant.
            var workOrder = WorkOrder.Create(
                PostgresFixture.TenantA,
                vehicle.Id,
                number: $"WO-{suffix}",
                title: "Replace brake line",
                description: null,
                WorkOrderPriority.High,
                WorkOrderSource.PreTripInspection,
                sourceRef: null,
                createdBy: "Dispatch",
                assignedTo: null,
                dueDate: null,
                lineItems: ["Brakes — Major"],
                shopId: null,
                authorizedLimitCad: null,
                budgetCode: null,
                dateRequiredOrOos: null).Value;

            var inspection = Inspection(
                PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, [Defect("Brakes"), Defect("Defroster")]);
            Assert.True(inspection.LinkWorkOrder(workOrder.Id).IsSuccess);

            writer.WorkOrders.Add(workOrder);
            writer.VehicleInspections.Add(inspection);
            await writer.SaveChangesAsync();

            inspectionId = inspection.Id;
            workOrderId = workOrder.Id;
        }

        // Still open while the repair is merely underway — the panel tags the work order but the
        // defects stay listed.
        var underway = await ReadAsync(vehicle.Id);
        Assert.Equal(2, underway.Count);
        Assert.All(underway, d =>
        {
            Assert.Equal(workOrderId, d.WorkOrderId);
            Assert.Equal($"WO-{suffix}", d.WorkOrderNumber);
            Assert.Equal("Open", d.WorkOrderStatus);
            Assert.Null(d.ResolvedAtUtc);
        });

        // Complete through the real handler, on real repositories, in one transaction.
        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var handler = new CompleteWorkOrderCommandHandler(
                new WorkOrderRepository(writer),
                new ServiceRecordRepository(writer),
                new VehicleInspectionRepository(writer));

            var result = await handler.Handle(
                new CompleteWorkOrderCommand(
                    PostgresFixture.TenantA,
                    workOrderId,
                    Date: DateTimeOffset.UtcNow,
                    PerformedBy: "M. Cardinal",
                    Category: ServiceCategory.InspectionFix,
                    OdometerKm: 118_500,
                    ItemsChanged: ["Brake line"],
                    Reason: "DVIR defect",
                    PartsUsed: [new ServicePartInput("BRK-1140", 1)],
                    LaborHours: 2.5m,
                    CostCad: 410m,
                    Notes: null),
                CancellationToken.None);

            Assert.True(result.IsSuccess, $"Completion failed: {(result.IsFailure ? result.Error.Code : string.Empty)}");
        }

        Assert.Empty(await ReadAsync(vehicle.Id));

        var history = await ReadAsync(vehicle.Id, includeResolved: true);
        Assert.Equal(2, history.Count);
        Assert.All(history, d =>
        {
            Assert.Equal("RepairedUnderWorkOrder", d.ResolutionReason);
            Assert.Equal(workOrderId, d.ResolvedByWorkOrderId);
            Assert.Equal("M. Cardinal", d.ResolvedBy);
            Assert.NotNull(d.ResolvedAtUtc);
            Assert.Equal("Completed", d.WorkOrderStatus);
        });

        Assert.NotEqual(Guid.Empty, inspectionId);
    }

    [Fact]
    public async Task The_same_item_reported_again_after_being_cleared_carries_the_earlier_resolution()
    {
        var vehicle = await SeedVehicleAsync();
        var now = DateTimeOffset.UtcNow;
        var resolvedAt = now.AddDays(-20);
        Guid olderInspectionId;

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var older = Inspection(
                PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, [Defect("Defroster")],
                performedAt: now.AddDays(-30));

            Assert.True(older
                .ResolveDefect("Defroster", DefectResolutionReason.PreviouslyRepaired, null, "R. Ballantyne", resolvedAt)
                .IsSuccess);

            var newer = Inspection(
                PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, [Defect("defroster ")],
                performedAt: now.AddDays(-1));

            writer.VehicleInspections.Add(older);
            writer.VehicleInspections.Add(newer);
            await writer.SaveChangesAsync();

            olderInspectionId = older.Id;
        }

        var open = await ReadAsync(vehicle.Id);

        var recurred = Assert.Single(open);
        var recurrence = Assert.IsType<Application.Inspections.PreviousResolutionResponse>(recurred.Recurrence);
        Assert.Equal(olderInspectionId, recurrence.InspectionId);
        Assert.Equal("PreviouslyRepaired", recurrence.ResolutionReason);
        Assert.Equal(resolvedAt, recurrence.ResolvedAtUtc, TimeSpan.FromMilliseconds(1));
        // Inferred from the item name (trimmed, case-insensitive), not a deliberate re-report.
        Assert.False(recurrence.Explicit);
    }

    [Fact]
    public async Task Defects_are_ordered_out_of_service_then_major_then_minor()
    {
        var vehicle = await SeedVehicleAsync();

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            writer.VehicleInspections.Add(Inspection(
                PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, [
                    Defect("Wipers", InspectionDefectSeverity.Minor),
                    Defect("Brakes", InspectionDefectSeverity.OutOfService),
                    Defect("Defroster", InspectionDefectSeverity.Major),
                ]));
            await writer.SaveChangesAsync();
        }

        var defects = await ReadAsync(vehicle.Id);

        Assert.Equal(["OutOfService", "Major", "Minor"], defects.Select(d => d.Severity));
        Assert.Equal(["Brakes", "Defroster", "Wipers"], defects.Select(d => d.Item));
    }

    [Fact]
    public async Task Amending_an_inspection_through_the_real_command_does_not_un_resolve_its_defects()
    {
        // The amend trap, end to end. This is the one that catches a correct domain with a broken
        // jsonb serialisation, or a command handler that drops the resolution fields on the way
        // through.
        var vehicle = await SeedVehicleAsync();
        var resolvedAt = DateTimeOffset.UtcNow.AddHours(-2);
        Guid inspectionId;

        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var inspection = Inspection(
                PostgresFixture.TenantA, vehicle.Id, vehicle.UnitNumber, [Defect("Brakes"), Defect("Defroster")]);

            Assert.True(inspection
                .ResolveDefect("Brakes", DefectResolutionReason.PreviouslyRepaired, "Fixed at Thompson", "R. Ballantyne", resolvedAt)
                .IsSuccess);

            writer.VehicleInspections.Add(inspection);
            await writer.SaveChangesAsync();
            inspectionId = inspection.Id;
        }

        // A dispatcher corrects the odometer and re-sends the defects exactly as the PUT body
        // carries them: item, severity, note — and no resolution fields at all.
        await using (var writer = fixture.CreateContext(PostgresFixture.TenantA))
        {
            var handler = new UpdateInspectionCommandHandler(new VehicleInspectionRepository(writer));

            var result = await handler.Handle(
                new UpdateInspectionCommand(
                    inspectionId,
                    InspectionSource.Dispatcher,
                    vehicle.Id,
                    vehicle.UnitNumber,
                    DriverName: "J. Spence",
                    EnteredBy: null,
                    PerformedAt: DateTimeOffset.UtcNow,
                    OdometerKm: 118_999,
                    Checklist: [],
                    Defects: [
                        new DefectInput("Brakes", InspectionDefectSeverity.Major, "as found"),
                        new DefectInput("Defroster", InspectionDefectSeverity.Major, "as found"),
                    ],
                    Weather: [],
                    TemperatureC: null,
                    RoadConditions: [],
                    Visibility: null,
                    RoadAdvisories: null,
                    FuelLevel: null,
                    Issues: [],
                    Attestations: [],
                    DriverSignatureName: null,
                    CertifiedAt: null,
                    FuelAdded: false,
                    FuelLitres: null,
                    FuelCostCad: null),
                CancellationToken.None);

            Assert.True(result.IsSuccess, $"Amend failed: {(result.IsFailure ? result.Error.Code : string.Empty)}");
        }

        var open = await ReadAsync(vehicle.Id);
        Assert.Equal("Defroster", Assert.Single(open).Item);

        var history = await ReadAsync(vehicle.Id, includeResolved: true);
        var brakes = history.Single(d => d.Item == "Brakes");
        Assert.Equal("PreviouslyRepaired", brakes.ResolutionReason);
        Assert.Equal("Fixed at Thompson", brakes.ResolutionNote);
        Assert.Equal("R. Ballantyne", brakes.ResolvedBy);
        Assert.Equal(resolvedAt, brakes.ResolvedAtUtc!.Value, TimeSpan.FromMilliseconds(1));
    }
}
