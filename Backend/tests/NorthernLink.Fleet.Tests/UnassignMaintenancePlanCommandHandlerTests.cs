using NorthernLink.Fleet.Application.Maintenance.Assignments.Unassign;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Domain.Maintenance.Events;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class UnassignMaintenancePlanCommandHandlerTests
{
    [Fact]
    public async Task Removes_the_assignment_and_raises_the_unassigned_event_first()
    {
        var assignments = new InMemoryPlanAssignmentRepository();
        var vehicleId = Guid.NewGuid();
        var assignment = PlanAssignment.Assign(TestVehicles.TenantId, vehicleId, Guid.NewGuid()).Value;
        assignments.Assignments.Add(assignment);
        var handler = new UnassignMaintenancePlanCommandHandler(assignments);

        var result = await handler.Handle(
            new UnassignMaintenancePlanCommand(TestVehicles.TenantId, vehicleId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(assignments.Assignments);
        var removed = Assert.Single(assignments.Removed);
        Assert.Contains(removed.DomainEvents, e => e is PlanUnassignedDomainEvent);
        Assert.Equal(1, assignments.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_missing_assignment_fails_with_not_found()
    {
        var assignments = new InMemoryPlanAssignmentRepository();
        var handler = new UnassignMaintenancePlanCommandHandler(assignments);

        var result = await handler.Handle(
            new UnassignMaintenancePlanCommand(TestVehicles.TenantId, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(MaintenanceErrors.AssignmentNotFound, result.Error);
        Assert.Equal(0, assignments.SaveChangesCallCount);
    }
}
