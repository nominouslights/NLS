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
}
