using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Domain.WorkOrders;
using NorthernLink.Fleet.Domain.WorkOrders.Events;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The WorkOrder side of the per-defect link: lines are fixed at creation, keyed by
/// (InspectionId, Item), and every line needs exactly one valid outcome at completion.
/// </summary>
public class WorkOrderDefectLinesTests
{
    private static readonly Guid InspectionA = Guid.NewGuid();
    private static readonly Guid InspectionB = Guid.NewGuid();

    private static WorkOrderDefectLine Line(
        Guid inspectionId,
        string item,
        InspectionDefectSeverity severity = InspectionDefectSeverity.Major) =>
        new() { InspectionId = inspectionId, Item = item, Severity = severity, Note = "as found" };

    private static Shared.Kernel.Result<WorkOrder> Create(IReadOnlyList<WorkOrderDefectLine>? defects) =>
        WorkOrder.Create(
            TestVehicles.TenantId,
            Guid.NewGuid(),
            "WO-T1",
            "Repair DVIR defects",
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
            defects);

    private static WorkOrderDefectOutcome Outcome(
        Guid inspectionId, string item, DefectRepairOutcome outcome, string? note = null) =>
        new(inspectionId, item, outcome, note);

    [Fact]
    public void The_same_defect_twice_is_rejected_case_insensitively()
    {
        var result = Create([Line(InspectionA, "Brakes"), Line(InspectionA, " brakes ")]);

        Assert.True(result.IsFailure);
        Assert.Equal(WorkOrderErrors.DuplicateDefect, result.Error);
    }

    [Fact]
    public void The_same_item_on_two_different_inspections_is_two_defects()
    {
        var result = Create([Line(InspectionA, "Brakes"), Line(InspectionB, "Brakes")]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Defects.Count);
        Assert.True(result.Value.HasDefectLines);
    }

    [Fact]
    public void A_line_with_no_item_is_rejected()
    {
        var result = Create([Line(InspectionA, "  ")]);

        Assert.Equal(WorkOrderErrors.DefectItemRequired, result.Error);
    }

    [Fact]
    public void Outcomes_cannot_be_smuggled_in_at_creation()
    {
        var result = Create([Line(InspectionA, "Brakes") with { Outcome = DefectRepairOutcome.Repaired, OutcomeNote = "x" }]);

        var line = Assert.Single(result.Value.Defects);
        Assert.Null(line.Outcome);
        Assert.Null(line.OutcomeNote);
    }

    [Fact]
    public void Completing_records_one_outcome_per_line()
    {
        var workOrder = Create([
            Line(InspectionA, "Brakes"),
            Line(InspectionA, "Wipers", InspectionDefectSeverity.Minor),
            Line(InspectionB, "Horn"),
        ]).Value;
        var serviceId = Guid.NewGuid();

        var result = workOrder.Complete(serviceId, [
            Outcome(InspectionA, "brakes", DefectRepairOutcome.Repaired, " new pads "),
            Outcome(InspectionA, "Wipers", DefectRepairOutcome.Deferred, "parts on order"),
            Outcome(InspectionB, "Horn", DefectRepairOutcome.NoFaultFound),
        ]);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        Assert.Equal(serviceId, workOrder.ResolvingServiceId);

        Assert.Equal(DefectRepairOutcome.Repaired, workOrder.Defects[0].Outcome);
        Assert.Equal("new pads", workOrder.Defects[0].OutcomeNote);
        Assert.Equal(DefectRepairOutcome.Deferred, workOrder.Defects[1].Outcome);
        Assert.Equal("parts on order", workOrder.Defects[1].OutcomeNote);
        Assert.Equal(DefectRepairOutcome.NoFaultFound, workOrder.Defects[2].Outcome);
        Assert.Null(workOrder.Defects[2].OutcomeNote);

        Assert.Single(workOrder.DomainEvents.OfType<WorkOrderCompletedDomainEvent>());
    }

    [Fact]
    public void A_line_without_an_outcome_blocks_completion()
    {
        var workOrder = Create([Line(InspectionA, "Brakes"), Line(InspectionA, "Wipers")]).Value;

        var result = workOrder.Complete(Guid.NewGuid(), [Outcome(InspectionA, "Brakes", DefectRepairOutcome.Repaired)]);

        Assert.Equal(WorkOrderErrors.DefectOutcomeMissing, result.Error);
        AssertUnchanged(workOrder);
    }

    [Fact]
    public void No_outcomes_at_all_on_a_work_order_with_lines_blocks_completion()
    {
        var workOrder = Create([Line(InspectionA, "Brakes")]).Value;

        Assert.Equal(WorkOrderErrors.DefectOutcomeMissing, workOrder.Complete(Guid.NewGuid()).Error);
        AssertUnchanged(workOrder);
    }

    [Fact]
    public void An_outcome_for_a_defect_not_on_the_work_order_is_rejected()
    {
        var workOrder = Create([Line(InspectionA, "Brakes")]).Value;

        var result = workOrder.Complete(Guid.NewGuid(), [
            Outcome(InspectionA, "Brakes", DefectRepairOutcome.Repaired),
            Outcome(InspectionB, "Brakes", DefectRepairOutcome.Repaired),
        ]);

        Assert.Equal(WorkOrderErrors.UnknownDefectOutcome, result.Error);
        AssertUnchanged(workOrder);
    }

    [Fact]
    public void Two_outcomes_for_one_line_are_rejected()
    {
        var workOrder = Create([Line(InspectionA, "Brakes")]).Value;

        var result = workOrder.Complete(Guid.NewGuid(), [
            Outcome(InspectionA, "Brakes", DefectRepairOutcome.Repaired),
            Outcome(InspectionA, "BRAKES", DefectRepairOutcome.NoFaultFound),
        ]);

        Assert.Equal(WorkOrderErrors.DuplicateDefectOutcome, result.Error);
        AssertUnchanged(workOrder);
    }

    [Fact]
    public void An_out_of_range_outcome_value_is_rejected()
    {
        var workOrder = Create([Line(InspectionA, "Brakes")]).Value;

        var result = workOrder.Complete(Guid.NewGuid(), [Outcome(InspectionA, "Brakes", (DefectRepairOutcome)42)]);

        Assert.Equal(WorkOrderErrors.InvalidDefectOutcome, result.Error);
        AssertUnchanged(workOrder);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Deferring_needs_a_note(string? note)
    {
        var workOrder = Create([Line(InspectionA, "Wipers", InspectionDefectSeverity.Minor)]).Value;

        var result = workOrder.Complete(Guid.NewGuid(), [Outcome(InspectionA, "Wipers", DefectRepairOutcome.Deferred, note)]);

        Assert.Equal(WorkOrderErrors.DeferredNoteRequired, result.Error);
        AssertUnchanged(workOrder);
    }

    [Fact]
    public void An_out_of_service_defect_can_never_be_deferred_even_with_a_note()
    {
        var workOrder = Create([Line(InspectionA, "Brakes", InspectionDefectSeverity.OutOfService)]).Value;

        var result = workOrder.Complete(Guid.NewGuid(), [
            Outcome(InspectionA, "Brakes", DefectRepairOutcome.Deferred, "no parts in town"),
        ]);

        Assert.Equal(WorkOrderErrors.OutOfServiceCannotBeDeferred, result.Error);
        AssertUnchanged(workOrder);
    }

    [Fact]
    public void An_out_of_service_defect_can_be_found_not_faulty()
    {
        var workOrder = Create([Line(InspectionA, "Brakes", InspectionDefectSeverity.OutOfService)]).Value;

        var result = workOrder.Complete(Guid.NewGuid(), [Outcome(InspectionA, "Brakes", DefectRepairOutcome.NoFaultFound)]);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void A_work_order_without_lines_completes_as_before_and_accepts_no_outcomes()
    {
        var legacy = Create(null).Value;
        Assert.False(legacy.HasDefectLines);

        var withOutcome = legacy.Complete(Guid.NewGuid(), [Outcome(InspectionA, "Brakes", DefectRepairOutcome.Repaired)]);
        Assert.Equal(WorkOrderErrors.UnknownDefectOutcome, withOutcome.Error);
        AssertUnchanged(legacy);

        Assert.True(legacy.Complete(Guid.NewGuid()).IsSuccess);
        Assert.Equal(WorkOrderStatus.Completed, legacy.Status);
    }

    private static void AssertUnchanged(WorkOrder workOrder)
    {
        Assert.NotEqual(WorkOrderStatus.Completed, workOrder.Status);
        Assert.Null(workOrder.ResolvingServiceId);
        Assert.All(workOrder.Defects, l => Assert.Null(l.Outcome));
        Assert.Empty(workOrder.DomainEvents.OfType<WorkOrderCompletedDomainEvent>());
    }
}
