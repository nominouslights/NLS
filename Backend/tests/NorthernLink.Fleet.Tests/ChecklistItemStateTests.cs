using NorthernLink.Fleet.Domain.Inspections;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The NL-PTI-01 tri-state. Two things are load-bearing here and neither fails loudly on its own:
///
/// <list type="number">
/// <item>Rows written before this change carry only <c>Passed</c>, so <c>EffectiveState</c> has to
/// read the whole backlog back into the three-value vocabulary — otherwise every historical row
/// renders as blank or "unknown" on the screens built for the new form.</item>
/// <item>On write, <c>Passed</c> is <c>State != Defect</c>, NOT <c>State == Ok</c>. An N/A answer
/// is not a failure, and deriving it the wrong way turns every "does not apply to this unit" row
/// into a defect for every consumer still reading the bool.</item>
/// </list>
/// </summary>
public class ChecklistItemStateTests
{
    [Fact]
    public void A_legacy_passed_row_reads_back_as_Ok()
    {
        var item = TestInspections.ChecklistItem(passed: true, state: null);

        Assert.Null(item.State);
        Assert.Equal(ChecklistItemState.Ok, item.EffectiveState);
    }

    [Fact]
    public void A_legacy_failed_row_reads_back_as_Defect()
    {
        var item = TestInspections.ChecklistItem(passed: false, state: null);

        Assert.Null(item.State);
        Assert.Equal(ChecklistItemState.Defect, item.EffectiveState);
    }

    [Theory]
    [InlineData(ChecklistItemState.Ok)]
    [InlineData(ChecklistItemState.Defect)]
    [InlineData(ChecklistItemState.NotApplicable)]
    public void An_explicit_state_wins_over_the_passed_bool(ChecklistItemState state)
    {
        // Passed deliberately set to the "wrong" value for each state: the record itself must
        // never prefer the legacy field when the real answer is present.
        var item = TestInspections.ChecklistItem(passed: state == ChecklistItemState.Defect, state: state);

        Assert.Equal(state, item.EffectiveState);
    }

    [Fact]
    public void A_not_applicable_row_with_a_note_round_trips_through_entry()
    {
        var inspection = TestInspections.PreTrip(checklistItems: [
            TestInspections.ChecklistItem(
                item: "Wheelchair lift",
                state: ChecklistItemState.NotApplicable,
                note: "Unit has no lift fitted"),
        ]);

        var row = Assert.Single(inspection.ChecklistItems);
        Assert.Equal("Wheelchair lift", row.Item);
        Assert.Equal(ChecklistItemState.NotApplicable, row.State);
        Assert.Equal(ChecklistItemState.NotApplicable, row.EffectiveState);
        Assert.Equal("Unit has no lift fitted", row.Note);
    }

    [Fact]
    public void Passed_is_written_as_state_is_not_defect_so_a_not_applicable_row_is_not_a_failure()
    {
        var inspection = TestInspections.PreTrip(checklistItems: [
            // Each row arrives with Passed set to the OPPOSITE of what the state implies, so the
            // assertions below can only pass if the aggregate re-derived it.
            TestInspections.ChecklistItem(item: "Tires", passed: false, state: ChecklistItemState.Ok),
            TestInspections.ChecklistItem(item: "Defroster", passed: true, state: ChecklistItemState.Defect),
            TestInspections.ChecklistItem(item: "Wheelchair lift", passed: false, state: ChecklistItemState.NotApplicable),
        ]);

        Assert.True(inspection.ChecklistItems.Single(i => i.Item == "Tires").Passed);
        Assert.False(inspection.ChecklistItems.Single(i => i.Item == "Defroster").Passed);

        // The whole point: N/A must read back to a legacy consumer as "not a defect".
        Assert.True(inspection.ChecklistItems.Single(i => i.Item == "Wheelchair lift").Passed);
    }

    [Fact]
    public void An_amendment_derives_passed_the_same_way()
    {
        // The invariant lives in one place on the aggregate, so it has to hold on both write
        // paths — an amend that skipped it would flip N/A rows to failed on the next odometer fix.
        var inspection = TestInspections.PreTrip();

        var result = TestInspections.AmendWith(inspection, defects: [], checklistItems: [
            TestInspections.ChecklistItem(item: "Wheelchair lift", passed: false, state: ChecklistItemState.NotApplicable),
            TestInspections.ChecklistItem(item: "Defroster", passed: true, state: ChecklistItemState.Defect),
        ]);

        Assert.True(result.IsSuccess);
        Assert.True(inspection.ChecklistItems.Single(i => i.Item == "Wheelchair lift").Passed);
        Assert.False(inspection.ChecklistItems.Single(i => i.Item == "Defroster").Passed);
    }

    [Fact]
    public void A_row_that_supplies_no_state_keeps_the_passed_it_was_given()
    {
        // The Dispatcher modal still posts the two-value shape. Nothing may be invented for it:
        // State stays null and Passed stays exactly as sent.
        var inspection = TestInspections.PreTrip(checklistItems: [
            TestInspections.ChecklistItem(item: "Tires", passed: false, state: null),
        ]);

        var row = Assert.Single(inspection.ChecklistItems);
        Assert.Null(row.State);
        Assert.False(row.Passed);
    }
}
