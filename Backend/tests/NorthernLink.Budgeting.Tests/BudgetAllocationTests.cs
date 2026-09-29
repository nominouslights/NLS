using System.Globalization;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Allocations.Events;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// BudgetAllocation (a budget item) input rules — title, cost (lump sum or quantity × unit cost),
/// justification, classification, vendor, tags, notes — cent rounding, the computed amount, and
/// Update / CopyInto semantics. The cross-aggregate guards (editable period, active code, item in
/// this period and tenant) are the handlers' and live in
/// <see cref="CreateBudgetAllocationCommandHandlerTests"/> and
/// <see cref="UpdateBudgetAllocationCommandHandlerTests"/>.
/// </summary>
public class BudgetAllocationTests
{
    private static readonly Guid PeriodId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CodeId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static decimal Money(string amount) => decimal.Parse(amount, CultureInfo.InvariantCulture);

    private static Result<BudgetAllocation> Create(BudgetItemDetails details, Guid? actorId = null) =>
        BudgetAllocation.Create(TestBudgeting.TenantId, PeriodId, CodeId, "ZBB-CREW-01", details, actorId);

    private static Error ErrorOf(BudgetItemDetails details)
    {
        var result = Create(details);
        Assert.True(result.IsFailure, "Expected the item to be refused.");
        return result.Error;
    }

    private static BudgetAllocation Valid(BudgetItemDetails details)
    {
        var result = Create(details);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    // --- Create ---

    [Fact]
    public void Create_stores_every_field_and_stamps_the_actor_as_creator()
    {
        var line = Valid(TestBudgeting.Item(
            title: "  Winter tires, unit NL-04  ",
            amount: 1250.00m,
            justification: "  Two crew rotations a week.  ",
            spendType: BudgetSpendType.Capital,
            recurrence: BudgetRecurrence.Recurring,
            vendor: "  Kal Tire Thompson ",
            tags: ["tires", " winter "],
            priority: BudgetItemPriority.MustHave,
            assumptions: " Four tires at last year's price. ",
            consequence: " Unit NL-04 is parked from November. "));

        Assert.Equal(TestBudgeting.TenantId, line.TenantId);
        Assert.Equal(PeriodId, line.PeriodId);
        Assert.Equal(CodeId, line.BudgetCodeId);
        Assert.Equal("ZBB-CREW-01", line.Code);
        Assert.Equal("Winter tires, unit NL-04", line.Title);
        Assert.Equal(1250.00m, line.AmountCad);
        Assert.Null(line.Quantity);
        Assert.Null(line.UnitCostCad);
        Assert.Null(line.Unit);
        Assert.Equal("Two crew rotations a week.", line.Justification);
        Assert.Equal(BudgetSpendType.Capital, line.SpendType);
        Assert.Equal(BudgetRecurrence.Recurring, line.Recurrence);
        Assert.Equal("Kal Tire Thompson", line.Vendor);
        Assert.Equal(["tires", "winter"], line.Tags);
        Assert.Equal(BudgetItemPriority.MustHave, line.Priority);
        Assert.Equal("Four tires at last year's price.", line.Assumptions);
        Assert.Equal("Unit NL-04 is parked from November.", line.ConsequenceIfUnfunded);
        Assert.Null(line.CreatedBy);
        Assert.Null(line.ModifiedBy);
        Assert.Equal(line.CreatedAtUtc, line.UpdatedAtUtc);
    }

    [Fact]
    public void The_documented_defaults_are_Operating_OneTime_ShouldHave_with_no_tags()
    {
        var line = Valid(new BudgetItemDetails { Title = "Fuel", AmountCad = 10m, Justification = "Why." });

        Assert.Equal(BudgetSpendType.Operating, line.SpendType);
        Assert.Equal(BudgetRecurrence.OneTime, line.Recurrence);
        Assert.Equal(BudgetItemPriority.ShouldHave, line.Priority);
        Assert.Empty(line.Tags);
        Assert.Null(line.Vendor);
        Assert.Null(line.Assumptions);
        Assert.Null(line.ConsequenceIfUnfunded);
    }

    [Fact]
    public void Blank_optional_text_is_stored_as_null()
    {
        var line = Valid(TestBudgeting.Item(vendor: "   ", assumptions: "", consequence: "\t"));

        Assert.Null(line.Vendor);
        Assert.Null(line.Assumptions);
        Assert.Null(line.ConsequenceIfUnfunded);
    }

    [Fact]
    public void Create_raises_the_created_event_with_actor_code_title_and_amount()
    {
        var line = TestBudgeting.CreateAllocation(
            PeriodId, CodeId, "ZBB-FUEL-01", actorId: TestBudgeting.ActorId,
            details: TestBudgeting.Item(title: "Diesel", amount: 480.50m));

        var created = Assert.Single(line.DomainEvents.OfType<BudgetAllocationCreatedDomainEvent>());
        Assert.Equal(line.Id, created.AllocationId);
        Assert.Equal(TestBudgeting.TenantId, created.TenantId);
        Assert.Equal(PeriodId, created.PeriodId);
        Assert.Equal(CodeId, created.BudgetCodeId);
        Assert.Equal("ZBB-FUEL-01", created.Code);
        Assert.Equal("Diesel", created.Title);
        Assert.Equal(480.50m, created.AmountCad);
        Assert.Equal(TestBudgeting.ActorId, created.ActorId);
        Assert.Empty(line.DomainEvents.OfType<BudgetAllocationUpdatedDomainEvent>());
    }

    [Fact]
    public void Create_with_an_actor_stamps_CreatedBy_and_without_one_leaves_it_null()
    {
        Assert.Equal(TestBudgeting.ActorId, TestBudgeting.CreateAllocation(actorId: TestBudgeting.ActorId).CreatedBy);

        var line = TestBudgeting.CreateAllocation(actorId: null);
        Assert.Null(line.CreatedBy);
        Assert.Null(Assert.Single(line.DomainEvents.OfType<BudgetAllocationCreatedDomainEvent>()).ActorId);
    }

    // --- Title ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_title_is_rejected(string? title) =>
        Assert.Equal(BudgetAllocationErrors.TitleRequired, ErrorOf(TestBudgeting.Item(title: title)));

    [Fact]
    public void A_title_of_exactly_120_characters_is_allowed_and_121_is_not()
    {
        Assert.Equal(120, Valid(TestBudgeting.Item(title: new string('t', 120))).Title.Length);
        Assert.Equal(BudgetAllocationErrors.TitleTooLong, ErrorOf(TestBudgeting.Item(title: new string('t', 121))));
    }

    [Fact]
    public void Title_padding_does_not_count_toward_the_limit() =>
        Assert.Equal(new string('t', 120), Valid(TestBudgeting.Item(title: $"  {new string('t', 120)}  ")).Title);

    // --- Lump-sum amount ---

    [Fact]
    public void A_missing_amount_with_no_build_up_is_rejected() =>
        Assert.Equal(BudgetAllocationErrors.AmountRequired, ErrorOf(TestBudgeting.Item(amount: null)));

    [Theory]
    [InlineData("-0.01")]
    [InlineData("-1")]
    [InlineData("-999999999.99")]
    public void A_negative_amount_is_rejected(string amount) =>
        Assert.Equal(BudgetAllocationErrors.AmountNegative, ErrorOf(TestBudgeting.Item(amount: Money(amount))));

    [Theory]
    [InlineData("1000000000")]
    [InlineData("1000000000.00")]
    [InlineData("999999999.995")]
    public void An_amount_above_the_numeric_12_2_ceiling_is_rejected(string amount) =>
        Assert.Equal(BudgetAllocationErrors.AmountTooLarge, ErrorOf(TestBudgeting.Item(amount: Money(amount))));

    [Fact]
    public void The_ceiling_itself_is_allowed() =>
        Assert.Equal(999_999_999.99m, Valid(TestBudgeting.Item(amount: BudgetAllocation.AmountMax)).AmountCad);

    [Fact]
    public void Zero_is_a_valid_plan() =>
        // ZBB: "we plan to spend nothing here, and here is why" is a decision worth an item.
        Assert.Equal(0m, Valid(TestBudgeting.Item(amount: 0m)).AmountCad);

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
        var line = Valid(TestBudgeting.Item(amount: Money(input)));

        Assert.Equal(Money(stored), line.AmountCad);
        Assert.Equal(Money(stored), Assert.Single(line.DomainEvents.OfType<BudgetAllocationCreatedDomainEvent>()).AmountCad);
    }

    // --- Built-up cost: quantity × unit cost ---

    [Fact]
    public void Quantity_times_unit_cost_computes_the_amount_and_keeps_the_build_up()
    {
        var line = Valid(TestBudgeting.Item(amount: null, quantity: 12m, unitCost: 350m, unit: " month "));

        Assert.Equal(4200.00m, line.AmountCad);
        Assert.Equal(12m, line.Quantity);
        Assert.Equal(350m, line.UnitCostCad);
        Assert.Equal("month", line.Unit);
    }

    [Fact]
    public void A_sent_amount_is_ignored_when_the_item_is_built_up()
    {
        // The build-up is the plan. A console that left a stale lump sum in the payload must not
        // be able to store an amount that disagrees with quantity × unit cost.
        var line = Valid(TestBudgeting.Item(amount: 99999m, quantity: 3m, unitCost: 10m));

        Assert.Equal(30.00m, line.AmountCad);
    }

    [Fact]
    public void A_built_up_item_needs_no_amount_at_all() =>
        Assert.Equal(15m, Valid(TestBudgeting.Item(amount: null, quantity: 1.5m, unitCost: 10m)).AmountCad);

    [Theory]
    // q, u, stored q, stored u, amount — factors round half away from zero to 2 dp FIRST, then the
    // product rounds half away from zero to cents.
    [InlineData("1.5", "10.005", "1.50", "10.01", "15.02")]   // 1.50 × 10.01 = 15.015 → 15.02
    [InlineData("0.5", "0.05", "0.50", "0.05", "0.03")]       // 0.025 → 0.03 (away from zero)
    [InlineData("3", "0.335", "3.00", "0.34", "1.02")]
    [InlineData("2.345", "4", "2.35", "4.00", "9.40")]
    [InlineData("7", "0", "7.00", "0.00", "0.00")]            // a free unit is a valid plan
    public void The_computed_amount_rounds_to_cents_half_away_from_zero(
        string q, string u, string storedQ, string storedU, string amount)
    {
        var line = Valid(TestBudgeting.Item(amount: null, quantity: Money(q), unitCost: Money(u)));

        Assert.Equal(Money(storedQ), line.Quantity);
        Assert.Equal(Money(storedU), line.UnitCostCad);
        Assert.Equal(Money(amount), line.AmountCad);
        // The stored factors always reproduce the stored amount.
        Assert.Equal(line.AmountCad, BudgetAllocation.Round(line.Quantity!.Value * line.UnitCostCad!.Value));
    }

    [Fact]
    public void Quantity_and_unit_cost_are_both_or_neither()
    {
        Assert.Equal(
            BudgetAllocationErrors.QuantityWithoutUnitCost,
            ErrorOf(TestBudgeting.Item(amount: 100m, quantity: 2m)));
        Assert.Equal(
            BudgetAllocationErrors.QuantityWithoutUnitCost,
            ErrorOf(TestBudgeting.Item(amount: 100m, unitCost: 50m)));
        Assert.True(Create(TestBudgeting.Item(amount: 100m)).IsSuccess);
        Assert.True(Create(TestBudgeting.Item(amount: null, quantity: 2m, unitCost: 50m)).IsSuccess);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.004")]   // rounds to 0.00 — would be a zero-unit item in numeric(12,2)
    public void A_quantity_that_is_not_positive_is_rejected(string quantity) =>
        Assert.Equal(
            BudgetAllocationErrors.QuantityNotPositive,
            ErrorOf(TestBudgeting.Item(quantity: Money(quantity), unitCost: 10m)));

    [Fact]
    public void The_smallest_positive_quantity_is_allowed() =>
        Assert.Equal(0.01m, Valid(TestBudgeting.Item(quantity: 0.005m, unitCost: 100m)).Quantity);

    [Fact]
    public void A_negative_unit_cost_is_rejected_and_zero_is_allowed()
    {
        Assert.Equal(
            BudgetAllocationErrors.UnitCostNegative,
            ErrorOf(TestBudgeting.Item(quantity: 1m, unitCost: -0.01m)));
        Assert.True(Create(TestBudgeting.Item(quantity: 1m, unitCost: 0m)).IsSuccess);
    }

    [Fact]
    public void Quantity_and_unit_cost_share_the_numeric_12_2_ceiling()
    {
        Assert.Equal(
            BudgetAllocationErrors.QuantityTooLarge,
            ErrorOf(TestBudgeting.Item(quantity: 1_000_000_000m, unitCost: 0m)));
        Assert.Equal(
            BudgetAllocationErrors.UnitCostTooLarge,
            ErrorOf(TestBudgeting.Item(quantity: 1m, unitCost: 1_000_000_000m)));
        Assert.Equal(
            BudgetAllocation.AmountMax,
            Valid(TestBudgeting.Item(quantity: 1m, unitCost: BudgetAllocation.AmountMax)).AmountCad);
    }

    [Fact]
    public void A_computed_amount_over_the_ceiling_is_rejected() =>
        Assert.Equal(
            BudgetAllocationErrors.AmountTooLarge,
            ErrorOf(TestBudgeting.Item(quantity: 100_000m, unitCost: 10_000m)));

    // --- Unit ---

    [Fact]
    public void A_unit_of_32_characters_is_allowed_and_33_is_not()
    {
        Assert.Equal(32, Valid(TestBudgeting.Item(quantity: 1m, unitCost: 1m, unit: new string('u', 32))).Unit!.Length);
        Assert.Equal(
            BudgetAllocationErrors.UnitTooLong,
            ErrorOf(TestBudgeting.Item(quantity: 1m, unitCost: 1m, unit: new string('u', 33))));
    }

    [Fact]
    public void A_unit_on_a_lump_sum_is_dropped_not_refused()
    {
        // A unit describes a quantity. A console switching from built-up to lump sum may still
        // send the old unit; storing it would describe nothing.
        Assert.Null(Valid(TestBudgeting.Item(amount: 100m, unit: "month")).Unit);
    }

    // --- Justification ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void A_blank_justification_is_rejected(string? justification) =>
        Assert.Equal(
            BudgetAllocationErrors.JustificationRequired,
            ErrorOf(TestBudgeting.Item(justification: justification)));

    [Fact]
    public void A_justification_of_exactly_1000_characters_is_allowed_and_1001_is_not()
    {
        Assert.Equal(
            BudgetAllocation.JustificationMaxLength,
            Valid(TestBudgeting.Item(justification: new string('x', 1000))).Justification.Length);
        Assert.Equal(
            BudgetAllocationErrors.JustificationTooLong,
            ErrorOf(TestBudgeting.Item(justification: new string('x', 1001))));
    }

    [Fact]
    public void The_justification_is_trimmed_on_store_and_padding_does_not_count_toward_the_limit()
    {
        var body = new string('x', BudgetAllocation.JustificationMaxLength);

        Assert.Equal(body, Valid(TestBudgeting.Item(justification: $"  {body} \n")).Justification);
    }

    // --- Enums ---

    [Fact]
    public void Out_of_range_enum_values_are_rejected()
    {
        // A numeric 99 binds cleanly through JsonStringEnumConverter; only the domain stops it.
        Assert.Equal(
            BudgetAllocationErrors.SpendTypeInvalid,
            ErrorOf(TestBudgeting.Item(spendType: (BudgetSpendType)99)));
        Assert.Equal(
            BudgetAllocationErrors.RecurrenceInvalid,
            ErrorOf(TestBudgeting.Item(recurrence: (BudgetRecurrence)99)));
        Assert.Equal(
            BudgetAllocationErrors.PriorityInvalid,
            ErrorOf(TestBudgeting.Item(priority: (BudgetItemPriority)99)));
    }

    [Fact]
    public void The_enum_member_spellings_are_the_wire_contract()
    {
        // Stored and sent as names; the Budgeting console mirrors these exact strings.
        Assert.Equal(["Operating", "Capital"], Enum.GetNames<BudgetSpendType>());
        Assert.Equal(["OneTime", "Recurring"], Enum.GetNames<BudgetRecurrence>());
        // Declaration order is the rank the allocations read sorts by: MustHave first.
        Assert.Equal(["MustHave", "ShouldHave", "NiceToHave"], Enum.GetNames<BudgetItemPriority>());
    }

    // --- Vendor, assumptions, consequence ---

    [Fact]
    public void Vendor_is_capped_at_120_characters()
    {
        Assert.Equal(120, Valid(TestBudgeting.Item(vendor: new string('v', 120))).Vendor!.Length);
        Assert.Equal(BudgetAllocationErrors.VendorTooLong, ErrorOf(TestBudgeting.Item(vendor: new string('v', 121))));
    }

    [Fact]
    public void Assumptions_are_capped_at_1000_characters()
    {
        Assert.Equal(1000, Valid(TestBudgeting.Item(assumptions: new string('a', 1000))).Assumptions!.Length);
        Assert.Equal(
            BudgetAllocationErrors.AssumptionsTooLong,
            ErrorOf(TestBudgeting.Item(assumptions: new string('a', 1001))));
    }

    [Fact]
    public void The_consequence_if_unfunded_is_capped_at_1000_characters()
    {
        Assert.Equal(1000, Valid(TestBudgeting.Item(consequence: new string('c', 1000))).ConsequenceIfUnfunded!.Length);
        Assert.Equal(
            BudgetAllocationErrors.ConsequenceTooLong,
            ErrorOf(TestBudgeting.Item(consequence: new string('c', 1001))));
    }

    // --- Tags ---

    [Fact]
    public void Tags_are_trimmed_and_de_duplicated_case_insensitively_first_spelling_wins()
    {
        var line = Valid(TestBudgeting.Item(tags: [" Fuel ", "fuel", "FUEL", "winter", "Winter "]));

        Assert.Equal(["Fuel", "winter"], line.Tags);
    }

    [Fact]
    public void Ten_tags_are_allowed_and_eleven_are_not()
    {
        var ten = Enumerable.Range(1, 10).Select(i => $"tag{i}").ToList();
        var eleven = Enumerable.Range(1, 11).Select(i => $"tag{i}").ToList();

        Assert.Equal(10, Valid(TestBudgeting.Item(tags: ten)).Tags.Count);
        Assert.Equal(BudgetAllocationErrors.TooManyTags, ErrorOf(TestBudgeting.Item(tags: eleven)));
    }

    [Fact]
    public void The_tag_limit_counts_after_de_duplication()
    {
        // Twelve entries, ten distinct tags: "Fuel" and "fuel" are one tag.
        var tags = Enumerable.Range(1, 10).Select(i => $"tag{i}").Concat(["TAG1", " tag2 "]).ToList();

        Assert.Equal(10, Valid(TestBudgeting.Item(tags: tags)).Tags.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789012345678901234567890123")] // 33 characters
    public void A_blank_or_over_long_tag_is_rejected(string tag) =>
        Assert.Equal(BudgetAllocationErrors.TagInvalid, ErrorOf(TestBudgeting.Item(tags: ["ok", tag])));

    [Fact]
    public void A_null_tag_entry_is_rejected() =>
        Assert.Equal(BudgetAllocationErrors.TagInvalid, ErrorOf(TestBudgeting.Item(tags: ["ok", null!])));

    [Fact]
    public void Tags_of_1_and_32_characters_are_allowed()
    {
        var line = Valid(TestBudgeting.Item(tags: ["x", new string('t', 32), $"  {new string('p', 32)}  "]));

        Assert.Equal(3, line.Tags.Count);
    }

    // --- Validate ordering ---

    [Fact]
    public void Validate_reports_one_error_at_a_time_in_form_order()
    {
        // Title, then cost, then justification: a payload wrong in several ways reports the first.
        Assert.Equal(
            BudgetAllocationErrors.TitleRequired,
            BudgetAllocation.Validate(TestBudgeting.Item(title: null, amount: null, justification: null)).Error);
        Assert.Equal(
            BudgetAllocationErrors.AmountRequired,
            BudgetAllocation.Validate(TestBudgeting.Item(amount: null, justification: null)).Error);
        Assert.Equal(
            BudgetAllocationErrors.QuantityWithoutUnitCost,
            BudgetAllocation.Validate(TestBudgeting.Item(quantity: 1m, justification: "   ")).Error);
        Assert.Equal(
            BudgetAllocationErrors.AmountNegative,
            BudgetAllocation.Validate(TestBudgeting.Item(amount: -1m, justification: "   ")).Error);
        Assert.Equal(
            BudgetAllocationErrors.JustificationRequired,
            BudgetAllocation.Validate(TestBudgeting.Item(justification: "", vendor: new string('v', 121))).Error);
    }

    [Fact]
    public void Validate_passes_a_well_formed_input()
    {
        Assert.True(BudgetAllocation.Validate(TestBudgeting.Item(amount: 0m)).IsSuccess);
        Assert.True(BudgetAllocation.Validate(TestBudgeting.Item(
            amount: null, quantity: 12m, unitCost: 350m, unit: "month",
            tags: ["a"], vendor: "V", assumptions: "A", consequence: "C")).IsSuccess);
    }

    // --- Update ---

    [Fact]
    public void Update_rewrites_every_field_and_the_modifier_and_leaves_creator_period_and_id_alone()
    {
        var line = TestBudgeting.CreateAllocation(
            PeriodId, CodeId, "ZBB-CREW-01", 1250m, "Original reasoning.", TestBudgeting.ActorId);
        var editor = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var updatedBefore = line.UpdatedAtUtc;
        var id = line.Id;

        var result = line.Update(CodeId, "ZBB-CREW-01", TestBudgeting.Item(
            title: "Crew rotations, revised",
            amount: null,
            quantity: 26m,
            unitCost: 72.5m,
            unit: "rotation",
            justification: "  Revised after the Q3 actuals.  ",
            spendType: BudgetSpendType.Capital,
            recurrence: BudgetRecurrence.Recurring,
            vendor: "Alamos",
            tags: ["crew"],
            priority: BudgetItemPriority.NiceToHave,
            assumptions: "Two a week.",
            consequence: "Crew drives themselves."), editor);

        Assert.True(result.IsSuccess);
        Assert.Equal("Crew rotations, revised", line.Title);
        Assert.Equal(1885.00m, line.AmountCad);
        Assert.Equal(26m, line.Quantity);
        Assert.Equal(72.5m, line.UnitCostCad);
        Assert.Equal("rotation", line.Unit);
        Assert.Equal("Revised after the Q3 actuals.", line.Justification);
        Assert.Equal(BudgetSpendType.Capital, line.SpendType);
        Assert.Equal(BudgetRecurrence.Recurring, line.Recurrence);
        Assert.Equal("Alamos", line.Vendor);
        Assert.Equal(["crew"], line.Tags);
        Assert.Equal(BudgetItemPriority.NiceToHave, line.Priority);
        Assert.Equal("Two a week.", line.Assumptions);
        Assert.Equal("Crew drives themselves.", line.ConsequenceIfUnfunded);
        Assert.Equal(editor, line.ModifiedBy);
        Assert.Equal(TestBudgeting.ActorId, line.CreatedBy);
        Assert.Equal(PeriodId, line.PeriodId);
        Assert.Equal(id, line.Id);
        Assert.True(line.UpdatedAtUtc >= updatedBefore);
    }

    [Fact]
    public void Update_can_move_the_item_to_another_code_and_takes_that_codes_string()
    {
        var line = TestBudgeting.CreateAllocation(PeriodId, CodeId, "ZBB-CREW-01");
        var fuelId = Guid.NewGuid();

        Assert.True(line.Update(fuelId, "ZBB-FUEL-01", TestBudgeting.Item(), null).IsSuccess);

        Assert.Equal(fuelId, line.BudgetCodeId);
        Assert.Equal("ZBB-FUEL-01", line.Code);
    }

    [Fact]
    public void Switching_a_built_up_item_back_to_a_lump_sum_clears_the_build_up()
    {
        var line = TestBudgeting.CreateAllocation(
            details: TestBudgeting.Item(amount: null, quantity: 2m, unitCost: 5m, unit: "trip"));

        Assert.True(line.Update(CodeId, "ZBB-CREW-01", TestBudgeting.Item(amount: 75m, unit: "trip"), null).IsSuccess);

        Assert.Equal(75m, line.AmountCad);
        Assert.Null(line.Quantity);
        Assert.Null(line.UnitCostCad);
        Assert.Null(line.Unit);
    }

    [Fact]
    public void Update_raises_the_updated_event_with_code_title_rounded_amount_and_actor()
    {
        var line = TestBudgeting.CreateAllocation();
        line.ClearDomainEvents();
        var fuelId = Guid.NewGuid();

        line.Update(fuelId, "ZBB-FUEL-01", TestBudgeting.Item(title: "Diesel", amount: 10.005m), TestBudgeting.ActorId);

        var updated = Assert.Single(line.DomainEvents.OfType<BudgetAllocationUpdatedDomainEvent>());
        Assert.Equal(line.Id, updated.AllocationId);
        Assert.Equal(fuelId, updated.BudgetCodeId);
        Assert.Equal("ZBB-FUEL-01", updated.Code);
        Assert.Equal("Diesel", updated.Title);
        Assert.Equal(10.01m, updated.AmountCad);
        Assert.Equal(TestBudgeting.ActorId, updated.ActorId);
        Assert.Empty(line.DomainEvents.OfType<BudgetAllocationCreatedDomainEvent>());
    }

    [Fact]
    public void Update_with_unchanged_values_still_raises_the_event()
    {
        // A re-submitted item is still a decision the journal carries, and an eventless Modified
        // save is refused by the audit pipeline — so "nothing changed" must not mean "no event".
        var line = TestBudgeting.CreateAllocation(PeriodId, CodeId, details: TestBudgeting.Item());
        line.ClearDomainEvents();

        var result = line.Update(CodeId, "ZBB-CREW-01", TestBudgeting.Item(), null);

        Assert.True(result.IsSuccess);
        Assert.Single(line.DomainEvents.OfType<BudgetAllocationUpdatedDomainEvent>());
    }

    [Fact]
    public void A_refused_update_changes_nothing_and_raises_nothing()
    {
        var line = TestBudgeting.CreateAllocation(
            PeriodId, CodeId, "ZBB-CREW-01", 1250m, "Original.", actorId: null);
        line.ClearDomainEvents();

        foreach (var bad in new[]
        {
            TestBudgeting.Item(amount: -5m),
            TestBudgeting.Item(justification: "   "),
            TestBudgeting.Item(title: ""),
            TestBudgeting.Item(quantity: 1m),
            TestBudgeting.Item(tags: Enumerable.Range(0, 11).Select(i => $"t{i}").ToList()),
        })
        {
            Assert.True(line.Update(Guid.NewGuid(), "ZBB-OTHER-01", bad, TestBudgeting.ActorId).IsFailure);
        }

        Assert.Equal(1250m, line.AmountCad);
        Assert.Equal("Original.", line.Justification);
        Assert.Equal(CodeId, line.BudgetCodeId);
        Assert.Equal("ZBB-CREW-01", line.Code);
        Assert.Null(line.ModifiedBy);
        Assert.Empty(line.DomainEvents);
    }

    // --- CopyInto / NeedsJustification ---
    //
    // CopyInto is the one place the aggregate's headline invariant — every item is argued — is
    // broken on purpose: it carries the item forward as a starting position and drops the
    // argument, because last period's reasoning is not this period's reasoning. These tests exist
    // so that nobody "fixes" it back into Create.

    [Fact]
    public void CopyInto_carries_every_field_across_and_clears_only_the_justification()
    {
        var targetPeriod = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var source = TestBudgeting.CreateAllocation(
            PeriodId, CodeId, "ZBB-CREW-01", actorId: TestBudgeting.ActorId,
            details: TestBudgeting.Item(
                title: "Crew rotations",
                amount: null,
                quantity: 26m,
                unitCost: 72.5m,
                unit: "rotation",
                justification: "Last quarter's reasoning.",
                spendType: BudgetSpendType.Capital,
                recurrence: BudgetRecurrence.Recurring,
                vendor: "Alamos",
                tags: ["crew", "contract"],
                priority: BudgetItemPriority.MustHave,
                assumptions: "Two a week.",
                consequence: "Crew drives themselves."));

        var copy = source.CopyInto(targetPeriod, TestBudgeting.ActorId);

        Assert.Equal(TestBudgeting.TenantId, copy.TenantId);
        Assert.Equal(targetPeriod, copy.PeriodId);
        Assert.Equal(CodeId, copy.BudgetCodeId);
        Assert.Equal("ZBB-CREW-01", copy.Code);
        Assert.Equal("Crew rotations", copy.Title);
        Assert.Equal(1885.00m, copy.AmountCad);
        Assert.Equal(26m, copy.Quantity);
        Assert.Equal(72.5m, copy.UnitCostCad);
        Assert.Equal("rotation", copy.Unit);
        Assert.Equal(BudgetSpendType.Capital, copy.SpendType);
        Assert.Equal(BudgetRecurrence.Recurring, copy.Recurrence);
        Assert.Equal("Alamos", copy.Vendor);
        Assert.Equal(["crew", "contract"], copy.Tags);
        Assert.Equal(BudgetItemPriority.MustHave, copy.Priority);
        Assert.Equal("Two a week.", copy.Assumptions);
        Assert.Equal("Crew drives themselves.", copy.ConsequenceIfUnfunded);
        Assert.Equal(string.Empty, copy.Justification);
        Assert.True(copy.NeedsJustification);
    }

    [Fact]
    public void CopyInto_does_not_share_the_tag_list_with_the_source()
    {
        var source = TestBudgeting.CreateAllocation(details: TestBudgeting.Item(tags: ["fuel"]));

        var copy = source.CopyInto(Guid.NewGuid(), null);
        copy.Tags.Add("mutated");

        Assert.Equal(["fuel"], source.Tags);
    }

    [Fact]
    public void CopyInto_leaves_the_source_item_untouched()
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
    public void CopyInto_is_a_new_item_with_its_own_id_and_the_copier_as_creator()
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
        var targetPeriod = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var source = TestBudgeting.CreateAllocation(
            PeriodId, CodeId, "ZBB-FUEL-01", details: TestBudgeting.Item(title: "Diesel", amount: 480.50m));

        var copy = source.CopyInto(targetPeriod, TestBudgeting.ActorId);

        var created = Assert.Single(copy.DomainEvents.OfType<BudgetAllocationCreatedDomainEvent>());
        Assert.Equal(copy.Id, created.AllocationId);
        Assert.Equal(TestBudgeting.TenantId, created.TenantId);
        Assert.Equal(targetPeriod, created.PeriodId);
        Assert.Equal(CodeId, created.BudgetCodeId);
        Assert.Equal("ZBB-FUEL-01", created.Code);
        Assert.Equal("Diesel", created.Title);
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
        var copy = TestBudgeting.CreateAllocation(amount: 0m).CopyInto(Guid.NewGuid(), null);

        Assert.Equal(0m, copy.AmountCad);
        Assert.True(copy.NeedsJustification);
    }

    [Fact]
    public void A_copied_item_cannot_be_saved_again_until_it_is_argued()
    {
        // The whole point of the feature, at the aggregate level: Update runs the same Validate
        // that Create does, so a copied item is refused until somebody writes the argument.
        var copy = TestBudgeting.CreateAllocation(PeriodId, CodeId).CopyInto(Guid.NewGuid(), null);

        Assert.Equal(
            BudgetAllocationErrors.JustificationRequired,
            copy.Update(CodeId, "ZBB-CREW-01", TestBudgeting.Item(justification: ""), null).Error);
        Assert.Equal(
            BudgetAllocationErrors.JustificationRequired,
            copy.Update(CodeId, "ZBB-CREW-01", TestBudgeting.Item(justification: "   "), null).Error);
        Assert.True(copy.NeedsJustification);

        Assert.True(copy.Update(
            CodeId, "ZBB-CREW-01", TestBudgeting.Item(justification: "Argued fresh for this period."), null).IsSuccess);
        Assert.False(copy.NeedsJustification);
    }

    [Fact]
    public void A_item_created_the_normal_way_never_needs_a_justification()
    {
        Assert.False(TestBudgeting.CreateAllocation().NeedsJustification);
        Assert.False(TestBudgeting.CreateAllocation(justification: "  Argued.  ").NeedsJustification);
    }

    [Fact]
    public void Copying_a_copy_keeps_the_justification_empty()
    {
        var copy = TestBudgeting.CreateAllocation(amount: 900m).CopyInto(Guid.NewGuid(), null);

        var second = copy.CopyInto(Guid.NewGuid(), null);

        Assert.Equal(900m, second.AmountCad);
        Assert.True(second.NeedsJustification);
    }
}
