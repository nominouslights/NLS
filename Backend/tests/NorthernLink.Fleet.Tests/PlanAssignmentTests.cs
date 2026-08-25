using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Domain.Maintenance.Events;
using Xunit;

namespace NorthernLink.Fleet.Tests;

public class PlanAssignmentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid PlanId = Guid.NewGuid();

    [Fact]
    public void Assign_links_the_vehicle_to_the_plan_and_raises_the_assigned_event()
    {
        var result = PlanAssignment.Assign(TenantId, VehicleId, PlanId);

        Assert.True(result.IsSuccess);
        var assignment = result.Value;
        Assert.Equal(TenantId, assignment.TenantId);
        Assert.Equal(VehicleId, assignment.VehicleId);
        Assert.Equal(PlanId, assignment.PlanId);
        var evt = Assert.IsType<PlanAssignedDomainEvent>(Assert.Single(assignment.DomainEvents));
        Assert.Equal(assignment.Id, evt.AssignmentId);
        Assert.Equal(VehicleId, evt.VehicleId);
        Assert.Equal(PlanId, evt.PlanId);
        Assert.Equal(TenantId, evt.TenantId);
    }

    [Fact]
    public void Reassign_switches_the_plan_and_raises_the_assigned_event_again()
    {
        var assignment = PlanAssignment.Assign(TenantId, VehicleId, PlanId).Value;
        assignment.ClearDomainEvents();
        var newPlanId = Guid.NewGuid();

        var result = assignment.Reassign(newPlanId);

        Assert.True(result.IsSuccess);
        Assert.Equal(newPlanId, assignment.PlanId);
        var evt = Assert.IsType<PlanAssignedDomainEvent>(Assert.Single(assignment.DomainEvents));
        Assert.Equal(newPlanId, evt.PlanId);
    }

    [Fact]
    public void Reassigning_the_same_plan_is_a_noop_no_clock_restart_no_event()
    {
        var assignment = PlanAssignment.Assign(TenantId, VehicleId, PlanId).Value;
        assignment.ClearDomainEvents();
        var assignedAt = assignment.AssignedAtUtc;

        var result = assignment.Reassign(PlanId);

        Assert.True(result.IsSuccess);
        Assert.Equal(PlanId, assignment.PlanId);
        Assert.Equal(assignedAt, assignment.AssignedAtUtc);
        Assert.Empty(assignment.DomainEvents);
    }

    [Fact]
    public void MarkRemoved_raises_the_unassigned_event()
    {
        var assignment = PlanAssignment.Assign(TenantId, VehicleId, PlanId).Value;
        assignment.ClearDomainEvents();

        assignment.MarkRemoved();

        var evt = Assert.IsType<PlanUnassignedDomainEvent>(Assert.Single(assignment.DomainEvents));
        Assert.Equal(assignment.Id, evt.AssignmentId);
        Assert.Equal(VehicleId, evt.VehicleId);
        Assert.Equal(TenantId, evt.TenantId);
    }
}
