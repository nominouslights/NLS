using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Maintenance.Completions.Log;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>
/// The full log-a-completion flow end-to-end against real Postgres: the command handler's
/// cross-aggregate checks, the completion row, and the same-module odometer-propagation
/// reaction the projection worker dispatches from the journal (running under the journal
/// row's tenant via the ambient-tenant push) — which must advance the vehicle's master
/// odometer so a fresh completion cannot deflate every other line's due math.
/// </summary>
[Collection("postgres")]
public class PmCompletionFlowTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Logging_a_completion_persists_it_and_the_worker_reaction_advances_the_vehicle_odometer()
    {
        var worker = fixture.BuildFleetProjectionWorker();
        var tenant = Guid.NewGuid();

        var vehicle = TestVehicleFactory.Create(tenant, odometerKm: 100_000, endOfLifeKm: 1_000_000);
        var plan = PmTestData.Plan(
            tenant,
            PmTestData.UniqueName("Flow plan"),
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

        // Clear the setup backlog so the next poll carries exactly the completion event. A
        // single ProcessOnceAsync is not enough here: one poll takes at most BatchSize rows,
        // and earlier polls' secondary commands append journal rows after their checkpoint —
        // the fixture helper loops until the journal is genuinely quiescent.
        await fixture.DrainFleetProjectionsAsync();

        Guid completionId;
        await using (var context = fixture.CreateContext(tenant))
        {
            var handler = new LogPmCompletionCommandHandler(
                new PmCompletionRepository(context),
                new PlanAssignmentRepository(context),
                new MaintenancePlanRepository(context),
                new VehicleRepository(context),
                new WorkOrderRepository(context));

            // A code the assigned plan does not carry must fail loudly, not save quietly.
            var unknownCode = await handler.Handle(
                new LogPmCompletionCommand(
                    tenant, vehicle.Id, "PM-NOPE-001", PmEntryKind.Item, PmSchedule.TodayUtc(),
                    104_000, "R. Beardy", null, null, null),
                CancellationToken.None);
            Assert.True(unknownCode.IsFailure);
            Assert.Equal(MaintenanceErrors.CompletionCodeNotInPlan.Code, unknownCode.Error.Code);

            var logged = await handler.Handle(
                new LogPmCompletionCommand(
                    tenant, vehicle.Id, "PM-X-001", PmEntryKind.Item, PmSchedule.TodayUtc(),
                    104_000, "R. Beardy", null, null, null),
                CancellationToken.None);
            Assert.True(logged.IsSuccess, logged.IsFailure ? logged.Error.Code : null);
            completionId = logged.Value;
        }

        // One poll projects the completion AND dispatches PropagatePmOdometerCommand under
        // the journal row's tenant.
        await worker.ProcessOnceAsync(CancellationToken.None);

        await using (var reader = fixture.CreateContext(tenant))
        {
            var projected = await reader.PmCompletionReadModels.SingleAsync(c => c.Id == completionId);
            Assert.Equal(104_000, projected.OdometerKm);

            // The reaction went through the monotonic Vehicle.RecordOdometer: 104,000 is
            // ahead of the 100,000 registration reading, so the master odometer advanced.
            var updatedVehicle = await reader.Vehicles.SingleAsync(v => v.Id == vehicle.Id);
            Assert.Equal(104_000, updatedVehicle.OdometerKm);
        }
    }
}
