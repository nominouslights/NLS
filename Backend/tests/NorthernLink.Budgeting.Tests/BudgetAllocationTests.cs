using System.Globalization;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Allocations.Events;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// BudgetAllocation input rules (amount, justification), cent rounding, and Update semantics.
/// The cross-aggregate guards (editable period, active code) are the set handler's and live in
/// <see cref="SetBudgetAllocationCommandHandlerTests"/>.
/// </summary>
public class BudgetAllocationTests
{
    private static readonly Guid PeriodId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CodeId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static decimal Money(string amount) => decimal.Parse(amount, CultureInfo.InvariantCulture);

    // --- Create ---

    [Fact]
    public void Create_stores_the_line_and_stamps_the_actor_as_creator()
    {
        var result = BudgetAllocation.Create(
            TestBudgeting.TenantId, PeriodId, CodeId, "ZBB-CREW-01", 1250.00m,
            "Two crew rotations a week.", TestBudgeting.ActorId);

        Assert.True(result.IsSuccess);
        var line = result.Value;
        Assert.Equal(TestBudgeting.TenantId, line.TenantId);
        Assert.Equal(PeriodId, line.PeriodId);
        Assert.Equal(CodeId, line.BudgetCodeId);
        Assert.Equal("ZBB-CREW-01", line.Code);
        Assert.Equal(1250.00m, line.AmountCad);
        Assert.Equal("Two crew rotations a week.", line.Justification);
        Assert.Equal(TestBudgeting.ActorId, line.CreatedBy);
        Assert.Null(line.ModifiedBy);
        Assert.Equal(line.CreatedAtUtc, line.UpdatedAtUtc);
    }

    [Fact]
    public void Create_raises_the_created_event_with_actor_code_and_amount()
    {
        var line = TestBudgeting.CreateAllocation(
            PeriodId, CodeId, "ZBB-FUEL-01", 480.50m, actorId: TestBudgeting.ActorId);

        var created = Assert.Single(line.DomainEvents.OfType<BudgetAllocationCreatedDomainEvent>());
        Assert.Equal(line.Id, created.AllocationId);
        Assert.Equal(TestBudgeting.TenantId, created.TenantId);
        Assert.Equal(PeriodId, created.PeriodId);
        Assert.Equal(CodeId, created.BudgetCodeId);
        Assert.Equal("ZBB-FUEL-01", created.Code);
        Assert.Equal(480.50m, created.AmountCad);
        Assert.Equal(TestBudgeting.ActorId, created.ActorId);
        Assert.Empty(line.DomainEvents.OfType<BudgetAllocationUpdatedDomainEvent>());
    }

    [Fact]
    public void Create_without_an_actor_leaves_CreatedBy_null()
    {
        var line = TestBudgeting.CreateAllocation(actorId: null);

        Assert.Null(line.CreatedBy);
        Assert.Null(Assert.Single(line.DomainEvents.OfType<BudgetAllocationCreatedDomainEvent>()).ActorId);
    }

    // --- Amount ---

    [Fact]
    public void A_missing_amount_is_rejected()
    {
        var result = BudgetAllocation.Create(
            TestBudgeting.TenantId, PeriodId, CodeId, "ZBB-CREW-01", null, "Why.", null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.AmountRequired, result.Error);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("-1")]
    [InlineData("-999999999.99")]
    public void A_negative_amount_is_rejected(string amount)
    {
        var result = BudgetAllocation.Create(
            TestBudgeting.TenantId, PeriodId, CodeId, "ZBB-CREW-01", Money(amount), "Why.", null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.AmountNegative, result.Error);
    }

    [Theory]
    [InlineData("1000000000")]
    [InlineData("1000000000.00")]
    [InlineData("999999999.995")]
    public void An_amount_above_the_numeric_12_2_ceiling_is_rejected(string amount)
    {
        var result = BudgetAllocation.Create(
            TestBudgeting.TenantId, PeriodId, CodeId, "ZBB-CREW-01", Money(amount), "Why.", null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.AmountTooLarge, result.Error);
    }

    [Fact]
    public void The_ceiling_itself_is_allowed()
    {
        var line = TestBudgeting.CreateAllocation(amount: BudgetAllocation.AmountMax);

        Assert.Equal(999_999_999.99m, line.AmountCad);
    }

    [Fact]
    public void Zero_is_a_valid_plan()
    {
        // ZBB: "we plan to spend nothing here, and here is why" is a decision worth a line.
        var line = TestBudgeting.CreateAllocation(amount: 0m);

        Assert.Equal(0m, line.AmountCad);
    }

    [Theory]
    [InlineData("10.005", "10.01")]
    [InlineData("10.004", "10.00")]
    [InlineData("10.015", "10.02")]
    [InlineData("0.125", "0.13")]
    [InlineData("2.675", "2.68")]
    [InlineData("1250", "1250.00")]
    public void Amounts_are_rounded_to_cents_half_away_from_zero(string input, string stored)
    {
        // Half away from zero, not banker's rounding: 10.005 lands on 10.01 the way a bookkeeper
        // expects, and the same way Invoice.TotalCad reaches numeric(12,2).
        var line = TestBudgeting.CreateAllocation(amount: Money(input));

        Assert.Equal(Money(stored), line.AmountCad);
        Assert.Equal(Money(stored), Assert.Single(line.DomainEvents.OfType<BudgetAllocationCreatedDomainEvent>()).AmountCad);
    }

    // --- Justification ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void A_blank_justification_is_rejected(string? justification)
    {
        var result = BudgetAllocation.Create(
            TestBudgeting.TenantId, PeriodId, CodeId, "ZBB-CREW-01", 100m, justification, null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.JustificationRequired, result.Error);
    }

    [Fact]
    public void A_justification_over_1000_characters_is_rejected()
    {
        var result = BudgetAllocation.Create(
            TestBudgeting.TenantId, PeriodId, CodeId, "ZBB-CREW-01", 100m,
            new string('x', BudgetAllocation.JustificationMaxLength + 1), null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetAllocationErrors.JustificationTooLong, result.Error);
    }

    [Fact]
    public void A_justification_of_exactly_1000_characters_is_allowed()
    {
        var line = TestBudgeting.CreateAllocation(
            justification: new string('x', BudgetAllocation.JustificationMaxLength));

        Assert.Equal(BudgetAllocation.JustificationMaxLength, line.Justification.Length);
    }

    [Fact]
    public void The_justification_is_trimmed_on_store_and_padding_does_not_count_toward_the_limit()
    {
        var body = new string('x', BudgetAllocation.JustificationMaxLength);

        var line = TestBudgeting.CreateAllocation(justification: $"  {body} \n");

        Assert.Equal(body, line.Justification);
    }

    // --- Validate ordering ---

    [Fact]
    public void Validate_reports_the_amount_before_the_justification()
    {
        // The handler runs Validate before any lookup and the caller sees one error at a time:
        // the amount is checked first, so a payload wrong in both ways reports the amount.
        Assert.Equal(BudgetAllocationErrors.AmountRequired, BudgetAllocation.Validate(null, null).Error);
        Assert.Equal(BudgetAllocationErrors.AmountNegative, BudgetAllocation.Validate(-1m, "   ").Error);
        Assert.Equal(
            BudgetAllocationErrors.AmountTooLarge,
            BudgetAllocation.Validate(BudgetAllocation.AmountMax + 0.01m, new string('x', 1001)).Error);
        Assert.Equal(BudgetAllocationErrors.JustificationRequired, BudgetAllocation.Validate(0m, "").Error);
    }

    [Fact]
    public void Validate_passes_a_well_formed_input()
    {
        Assert.True(BudgetAllocation.Validate(0m, "Why.").IsSuccess);
        Assert.True(BudgetAllocation.Validate(BudgetAllocation.AmountMax, new string('x', 1000)).IsSuccess);
    }

    // --- Update ---

    [Fact]
    public void Update_rewrites_amount_justification_and_modifier_and_leaves_creator_and_code_alone()
    {
        var line = TestBudgeting.CreateAllocation(
            PeriodId, CodeId, "ZBB-CREW-01", 1250m, "Original reasoning.", TestBudgeting.ActorId);
        var editor = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var updatedBefore = line.UpdatedAtUtc;
        var id = line.Id;

        var result = line.Update(1875.5m, "  Revised after the Q3 actuals.  ", editor);

        Assert.True(result.IsSuccess);
        Assert.Equal(1875.50m, line.AmountCad);
        Assert.Equal("Revised after the Q3 actuals.", line.Justification);
        Assert.Equal(editor, line.ModifiedBy);
        Assert.Equal(TestBudgeting.ActorId, line.CreatedBy);
        Assert.Equal("ZBB-CREW-01", line.Code);
        Assert.Equal(CodeId, line.BudgetCodeId);
        Assert.Equal(PeriodId, line.PeriodId);
        Assert.Equal(id, line.Id);
        Assert.True(line.UpdatedAtUtc >= updatedBefore);
    }

    [Fact]
    public void Update_raises_the_updated_event_with_the_rounded_amount_and_actor()
    {
        var line = TestBudgeting.CreateAllocation();
        line.ClearDomainEvents();

        line.Update(10.005m, "Rounded up.", TestBudgeting.ActorId);

        var updated = Assert.Single(line.DomainEvents.OfType<BudgetAllocationUpdatedDomainEvent>());
        Assert.Equal(line.Id, updated.AllocationId);
        Assert.Equal(10.01m, updated.AmountCad);
        Assert.Equal(TestBudgeting.ActorId, updated.ActorId);
        Assert.Empty(line.DomainEvents.OfType<BudgetAllocationCreatedDomainEvent>());
    }

    [Fact]
    public void Update_with_unchanged_values_still_raises_the_event()
    {
        // A re-submitted line is still a decision the journal carries, and an eventless Modified
        // save is refused by the audit pipeline — so "nothing changed" must not mean "no event".
        var line = TestBudgeting.CreateAllocation(amount: 1250m, justification: "Same.");
        line.ClearDomainEvents();

        var result = line.Update(1250m, "Same.", null);

        Assert.True(result.IsSuccess);
        Assert.Single(line.DomainEvents.OfType<BudgetAllocationUpdatedDomainEvent>());
    }

    [Theory]
    [InlineData("-5", "Fine.")]
    [InlineData("5", "   ")]
    [InlineData("1000000000", "Fine.")]
    public void A_refused_update_changes_nothing_and_raises_nothing(string amount, string justification)
    {
        var line = TestBudgeting.CreateAllocation(amount: 1250m, justification: "Original.", actorId: null);
        line.ClearDomainEvents();

        var result = line.Update(Money(amount), justification, TestBudgeting.ActorId);

        Assert.True(result.IsFailure);
        Assert.Equal(1250m, line.AmountCad);
        Assert.Equal("Original.", line.Justification);
        Assert.Null(line.ModifiedBy);
        Assert.Empty(line.DomainEvents);
    }

    [Fact]
    public void Update_reports_the_same_errors_as_Create()
    {
        var line = TestBudgeting.CreateAllocation();

        Assert.Equal(BudgetAllocationErrors.AmountRequired, line.Update(null, "Why.", null).Error);
        Assert.Equal(BudgetAllocationErrors.AmountNegative, line.Update(-1m, "Why.", null).Error);
        Assert.Equal(BudgetAllocationErrors.AmountTooLarge, line.Update(BudgetAllocation.AmountMax + 0.01m, "Why.", null).Error);
        Assert.Equal(BudgetAllocationErrors.JustificationRequired, line.Update(1m, null, null).Error);
        Assert.Equal(BudgetAllocationErrors.JustificationTooLong, line.Update(1m, new string('x', 1001), null).Error);
    }

    // --- CopyInto / NeedsJustification ---
    //
    // CopyInto is the one place the aggregate's headline invariant — every line is argued — is
    // broken on purpose: it carries the amount forward as a starting position and drops the
    // argument, because last period's reasoning is not this period's reasoning. These tests exist
    // so that nobody "fixes" it back into Create.

    [Fact]
    public void CopyInto_carries_tenant_code_and_amount_across_and_clears_the_justification()
    {
        var targetPeriod = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var source = TestBudgeting.CreateAllocation(
            PeriodId, CodeId, "ZBB-CREW-01", 1250.00m, "Last quarter's reasoning.", TestBudgeting.ActorId);

        var copy = source.CopyInto(targetPeriod, TestBudgeting.ActorId);

        Assert.Equal(TestBudgeting.TenantId, copy.TenantId);
        Assert.Equal(targetPeriod, copy.PeriodId);
        Assert.Equal(CodeId, copy.BudgetCodeId);
        Assert.Equal("ZBB-CREW-01", copy.Code);
        Assert.Equal(1250.00m, copy.AmountCad);
        Assert.Equal(string.Empty, copy.Justification);
        Assert.True(copy.NeedsJustification);
    }

    [Fact]
    public void CopyInto_leaves_the_source_line_untouched()
    {
        var source = TestBudgeting.CreateAllocation(
            PeriodId, CodeId, justification: "Original reasoning.", actorId: TestBudgeting.ActorId);

        source.CopyInto(Guid.NewGuid(), null);

        Assert.Equal("Original reasoning.", source.Justification);
        Assert.Equal(PeriodId, source.PeriodId);
        Assert.False(source.NeedsJustification);
        Assert.Null(source.ModifiedBy);
    }

    [Fact]
    public void CopyInto_is_a_new_line_with_its_own_id_and_the_copier_as_creator()
    {
        var copier = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var source = TestBudgeting.CreateAllocation(actorId: TestBudgeting.ActorId);

        var copy = source.CopyInto(Guid.NewGuid(), copier);

        Assert.NotEqual(source.Id, copy.Id);
        // Whoever ran the copy is who put those numbers in the new period.
        Assert.Equal(copier, copy.CreatedBy);
        Assert.Null(copy.ModifiedBy);
        Assert.Equal(copy.CreatedAtUtc, copy.UpdatedAtUtc);
    }

    [Fact]
    public void CopyInto_raises_the_created_event_like_Create_does()
    {
        // The read model is built from this event — a copy that raised nothing would leave the
        // console showing a period with no lines in it.
        var targetPeriod = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var source = TestBudgeting.CreateAllocation(PeriodId, CodeId, "ZBB-FUEL-01", 480.50m);

        var copy = source.CopyInto(targetPeriod, TestBudgeting.ActorId);

        var created = Assert.Single(copy.DomainEvents.OfType<BudgetAllocationCreatedDomainEvent>());
        Assert.Equal(copy.Id, created.AllocationId);
        Assert.Equal(TestBudgeting.TenantId, created.TenantId);
        Assert.Equal(targetPeriod, created.PeriodId);
        Assert.Equal(CodeId, created.BudgetCodeId);
        Assert.Equal("ZBB-FUEL-01", created.Code);
        Assert.Equal(480.50m, created.AmountCad);
        Assert.Equal(TestBudgeting.ActorId, created.ActorId);
        Assert.Empty(copy.DomainEvents.OfType<BudgetAllocationUpdatedDomainEvent>());
    }

    [Fact]
    public void CopyInto_without_an_actor_leaves_CreatedBy_null()
    {
        var copy = TestBudgeting.CreateAllocation().CopyInto(Guid.NewGuid(), null);

        Assert.Null(copy.CreatedBy);
        Assert.Null(Assert.Single(copy.DomainEvents.OfType<BudgetAllocationCreatedDomainEvent>()).ActorId);
    }

    [Fact]
    public void CopyInto_carries_a_zero_amount_as_is()
    {
        // "We plan to spend nothing here" is a plan, and copying it forward is meaningful.
        var copy = TestBudgeting.CreateAllocation(amount: 0m).CopyInto(Guid.NewGuid(), null);

        Assert.Equal(0m, copy.AmountCad);
        Assert.True(copy.NeedsJustification);
    }

    [Fact]
    public void A_copied_line_cannot_be_saved_again_until_it_is_argued()
    {
        // The whole point of the feature, at the aggregate level: Update runs the same Validate
        // that Create does, so a copied line is refused until somebody writes the argument.
        var copy = TestBudgeting.CreateAllocation(amount: 1250m).CopyInto(Guid.NewGuid(), null);

        Assert.Equal(BudgetAllocationErrors.JustificationRequired, copy.Update(1250m, "", null).Error);
        Assert.Equal(BudgetAllocationErrors.JustificationRequired, copy.Update(1250m, "   ", null).Error);
        Assert.True(copy.NeedsJustification);

        Assert.True(copy.Update(1250m, "Argued fresh for this period.", null).IsSuccess);
        Assert.False(copy.NeedsJustification);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void A_whitespace_justification_never_clears_NeedsJustification(string justification)
    {
        // NeedsJustification reads Length, and Validate rejects whitespace before it can ever be
        // stored — so there is no path to a line that is blank-looking but reads as argued. If
        // this ever fails, Validate stopped using IsNullOrWhiteSpace.
        var copy = TestBudgeting.CreateAllocation().CopyInto(Guid.NewGuid(), null);

        Assert.True(copy.Update(100m, justification, null).IsFailure);
        Assert.True(copy.NeedsJustification);
        Assert.Equal(string.Empty, copy.Justification);
    }

    [Fact]
    public void A_line_created_the_normal_way_never_needs_a_justification()
    {
        Assert.False(TestBudgeting.CreateAllocation().NeedsJustification);
        // Not even one padded out to nothing but whitespace — Create refuses that outright.
        Assert.False(TestBudgeting.CreateAllocation(justification: "  Argued.  ").NeedsJustification);
    }

    [Fact]
    public void Copying_a_copy_keeps_the_justification_empty()
    {
        // Chained copies must not accumulate anything: still no argument, still the same amount.
        var copy = TestBudgeting.CreateAllocation(amount: 900m).CopyInto(Guid.NewGuid(), null);

        var second = copy.CopyInto(Guid.NewGuid(), null);

        Assert.Equal(900m, second.AmountCad);
        Assert.True(second.NeedsJustification);
    }
}
