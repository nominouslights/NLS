using NorthernLink.Fleet.Domain.Inspections;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The interim per-defect link: which defects a work order's line items cover, matched against
/// the format the Dispatcher's prefill writes — <c>"{item} — {severity}[: {note}]"</c>
/// (<c>Dispatcher/lib/inspectionWorkOrder.ts</c>).
/// </summary>
public class DefectWorkOrderCoverageTests
{
    private static readonly DateTimeOffset ResolvedAt = new(2026, 3, 14, 17, 5, 0, TimeSpan.Zero);

    private static InspectionDefect Defect(string item) =>
        new() { Item = item, Severity = InspectionDefectSeverity.Major, Note = "as found" };

    private static VehicleInspection Inspection(params string[] items) =>
        TestInspections.PreTrip(defects: [.. items.Select(Defect)]);

    [Theory]
    [InlineData("Brakes — Major")]
    [InlineData("Brakes — Out-of-Service: weeping line")]
    [InlineData("  brakes   —   minor  ")]
    [InlineData("BRAKES")]
    [InlineData("Brakes - Major")]
    [InlineData("Brakes – Major")]
    [InlineData("Brakes: weeping line")]
    public void A_prefill_shaped_line_item_covers_its_defect(string lineItem)
    {
        var inspection = Inspection("Brakes", "Defroster");

        Assert.Equal(["Brakes"], inspection.DefectItemsCoveredBy([lineItem])!);
    }

    [Fact]
    public void The_item_key_is_compared_with_inner_whitespace_collapsed()
    {
        var inspection = Inspection("Tires  & wheels", "Defroster");

        Assert.Equal(["Tires  & wheels"], inspection.DefectItemsCoveredBy(["tires & WHEELS — Major"])!);
    }

    [Theory]
    [InlineData("Brake line replacement")]
    [InlineData("Brakes pads — Major")]
    [InlineData("Check brakes — Major")]
    public void An_item_that_is_only_a_word_prefix_or_appears_mid_line_is_not_a_match(string lineItem)
    {
        var inspection = Inspection("Brakes", "Defroster");

        Assert.Null(inspection.DefectItemsCoveredBy([lineItem]));
    }

    [Fact]
    public void When_one_item_prefixes_another_the_longest_match_wins()
    {
        var inspection = Inspection("Mirrors", "Mirrors - left");

        Assert.Equal(["Mirrors - left"], inspection.DefectItemsCoveredBy(["Mirrors - left — Minor"])!);
        Assert.Equal(["Mirrors"], inspection.DefectItemsCoveredBy(["Mirrors — Minor"])!);
    }

    [Fact]
    public void Free_text_line_items_that_name_no_defect_return_null_for_resolve_all()
    {
        var inspection = Inspection("Brakes", "Defroster");

        Assert.Null(inspection.DefectItemsCoveredBy(["Replace brake line", "Road test"]));
        Assert.Null(inspection.DefectItemsCoveredBy([]));
    }

    [Fact]
    public void Free_text_lines_alongside_a_matching_line_do_not_widen_the_coverage()
    {
        var inspection = Inspection("Brakes", "Defroster");

        Assert.Equal(["Brakes"], inspection.DefectItemsCoveredBy(["Brakes — Major", "Road test after repair"])!);
    }

    [Fact]
    public void Resolving_for_a_work_order_with_an_item_set_stamps_only_those_items()
    {
        var inspection = Inspection("Brakes", "Defroster", "Wipers");
        var workOrderId = Guid.NewGuid();

        inspection.ResolveDefectsForWorkOrder(workOrderId, "M. Cardinal", ResolvedAt, [" defroster "]);

        var defroster = inspection.Defects.Single(d => d.Item == "Defroster");
        Assert.Equal(DefectResolutionReason.RepairedUnderWorkOrder, defroster.ResolutionReason);
        Assert.Equal(workOrderId, defroster.ResolvedByWorkOrderId);
        Assert.False(inspection.Defects.Single(d => d.Item == "Brakes").IsResolved);
        Assert.False(inspection.Defects.Single(d => d.Item == "Wipers").IsResolved);
    }

    [Fact]
    public void An_item_set_naming_only_resolved_defects_is_a_no_op_with_no_event()
    {
        var inspection = Inspection("Brakes", "Defroster");
        Assert.True(inspection
            .ResolveDefect("Brakes", DefectResolutionReason.PreviouslyRepaired, null, "Dispatch", ResolvedAt)
            .IsSuccess);
        inspection.ClearDomainEvents();

        inspection.ResolveDefectsForWorkOrder(Guid.NewGuid(), "M. Cardinal", ResolvedAt, ["Brakes"]);

        Assert.Equal("Dispatch", inspection.Defects.Single(d => d.Item == "Brakes").ResolvedBy);
        Assert.False(inspection.Defects.Single(d => d.Item == "Defroster").IsResolved);
        Assert.Empty(inspection.DomainEvents);
    }
}
