using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Codes.Events;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>BudgetCode factory validation, code normalization, editing, retirement, and audit stamps.</summary>
public class BudgetCodeTests
{
    [Fact]
    public void Create_starts_active_and_keeps_every_detail()
    {
        var details = TestBudgeting.CodeDetails(
            name: "Alamos crew shuttle",
            category: BudgetCodeCategory.Revenue,
            reviewFrequency: BudgetReviewFrequency.Monthly,
            serviceLine: BudgetServiceLine.ContractCrew,
            costCentre: "OPS-01",
            glAccountCode: "4000",
            taxTreatment: BudgetTaxTreatment.GstApplicable,
            description: "Contracted crew rotation runs.");

        var result = BudgetCode.Create(TestBudgeting.TenantId, "ZBB-CREW-01", details, TestBudgeting.ActorId);

        Assert.True(result.IsSuccess);
        var code = result.Value;
        Assert.True(code.IsActive);
        Assert.Equal("ZBB-CREW-01", code.Code);
        Assert.Equal("Alamos crew shuttle", code.Name);
        Assert.Equal(BudgetCodeCategory.Revenue, code.Category);
        Assert.Equal(BudgetReviewFrequency.Monthly, code.ReviewFrequency);
        Assert.Equal(BudgetServiceLine.ContractCrew, code.ServiceLine);
        Assert.Equal("OPS-01", code.CostCentre);
        Assert.Equal("4000", code.GlAccountCode);
        Assert.Equal(BudgetTaxTreatment.GstApplicable, code.TaxTreatment);
        Assert.Equal("Contracted crew rotation runs.", code.Description);
    }

    [Fact]
    public void Only_code_name_category_and_review_frequency_are_required()
    {
        // The story's acceptance criterion, as an executable statement: everything optional is
        // genuinely optional, not merely nullable in the type.
        var bare = new BudgetCodeDetails { Name = "Fuel" };

        var result = BudgetCode.Create(TestBudgeting.TenantId, "FUEL", bare, actorId: null);

        Assert.True(result.IsSuccess);
        var code = result.Value;
        Assert.Null(code.Description);
        Assert.Null(code.ServiceLine);
        Assert.Null(code.CostCentre);
        Assert.Null(code.ParentCodeId);
        Assert.Null(code.GlAccountCode);
        Assert.Null(code.TaxTreatment);
        Assert.Null(code.BudgetOwnerUserId);
        Assert.Equal(BudgetReviewFrequency.Quarterly, code.ReviewFrequency);
    }

    [Theory]
    [InlineData("zbb-crew-01")]
    [InlineData("  ZBB-CREW-01  ")]
    [InlineData("Zbb-Crew-01")]
    public void Create_normalizes_the_code_to_trimmed_upper_case(string input)
    {
        var result = BudgetCode.Create(TestBudgeting.TenantId, input, TestBudgeting.CodeDetails(), actorId: null);

        Assert.True(result.IsSuccess);
        Assert.Equal("ZBB-CREW-01", result.Value.Code);
    }

    [Fact]
    public void Create_raises_the_created_event_carrying_the_normalized_code()
    {
        var code = TestBudgeting.CreateCode("fleet-maint");

        var created = Assert.Single(code.DomainEvents.OfType<BudgetCodeCreatedDomainEvent>());
        Assert.Equal(code.Id, created.BudgetCodeId);
        Assert.Equal(TestBudgeting.TenantId, created.TenantId);
        Assert.Equal("FLEET-MAINT", created.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_code_is_rejected(string input)
    {
        var result = BudgetCode.Create(TestBudgeting.TenantId, input, TestBudgeting.CodeDetails(), actorId: null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.CodeRequired, result.Error);
    }

    [Fact]
    public void A_code_longer_than_the_maximum_is_rejected()
    {
        var result = BudgetCode.Create(
            TestBudgeting.TenantId, new string('A', BudgetCode.CodeMaxLength + 1),
            TestBudgeting.CodeDetails(), actorId: null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.CodeTooLong, result.Error);
    }

    [Theory]
    [InlineData("-LEADING")]
    [InlineData("TRAILING-")]
    [InlineData("HAS SPACE")]
    [InlineData("HAS_UNDERSCORE")]
    [InlineData("HAS/SLASH")]
    public void A_malformed_code_is_rejected(string input)
    {
        var result = BudgetCode.Create(TestBudgeting.TenantId, input, TestBudgeting.CodeDetails(), actorId: null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.CodeInvalidFormat, result.Error);
    }

    [Fact]
    public void A_plain_alphanumeric_code_with_no_hyphen_is_allowed()
    {
        var result = BudgetCode.Create(
            TestBudgeting.TenantId, "FUEL01", TestBudgeting.CodeDetails(), actorId: null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void A_blank_name_is_rejected()
    {
        var result = BudgetCode.Create(
            TestBudgeting.TenantId, "ZBB-CREW-01", TestBudgeting.CodeDetails(name: "  "), actorId: null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.NameRequired, result.Error);
    }

    [Fact]
    public void A_blank_description_is_stored_as_null_and_is_no_longer_required()
    {
        // A deliberate behaviour flip from the first slice, where this field was the required
        // zero-based justification. Architecture §5.3 puts the per-period justification on the
        // allocation; what stays here is an optional standing note.
        var code = TestBudgeting.CreateCode(details: TestBudgeting.CodeDetails(description: "   "));

        Assert.Null(code.Description);
    }

    [Theory]
    [InlineData("costCentre")]
    [InlineData("glAccountCode")]
    public void Blank_optional_strings_are_stored_as_null_never_empty(string field)
    {
        var details = field == "costCentre"
            ? TestBudgeting.CodeDetails(costCentre: "  ")
            : TestBudgeting.CodeDetails(glAccountCode: "  ");

        var code = TestBudgeting.CreateCode(details: details);

        Assert.Null(field == "costCentre" ? code.CostCentre : code.GlAccountCode);
    }

    [Fact]
    public void An_over_long_description_is_rejected()
    {
        var result = BudgetCode.Create(
            TestBudgeting.TenantId,
            "ZBB-CREW-01",
            TestBudgeting.CodeDetails(description: new string('x', BudgetCode.DescriptionMaxLength + 1)),
            actorId: null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.DescriptionTooLong, result.Error);
    }

    [Fact]
    public void An_over_long_cost_centre_is_rejected()
    {
        var result = BudgetCode.Create(
            TestBudgeting.TenantId,
            "ZBB-CREW-01",
            TestBudgeting.CodeDetails(costCentre: new string('x', BudgetCode.CostCentreMaxLength + 1)),
            actorId: null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.CostCentreTooLong, result.Error);
    }

    [Fact]
    public void An_over_long_gl_account_code_is_rejected()
    {
        // Length is the ONLY thing checked on a GL code. QuickBooks work is manual by decision —
        // there is no synced chart of accounts to validate against, and this test exists partly
        // to record that the omission is intentional.
        var result = BudgetCode.Create(
            TestBudgeting.TenantId,
            "ZBB-CREW-01",
            TestBudgeting.CodeDetails(glAccountCode: new string('9', BudgetCode.GlAccountCodeMaxLength + 1)),
            actorId: null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.GlAccountCodeTooLong, result.Error);
    }

    [Fact]
    public void An_out_of_range_review_frequency_is_rejected()
    {
        // Reachable only through a numeric payload: JsonStringEnumConverter rejects an unknown
        // enum *name* before a command is built, but binds a numeric 99 cleanly.
        var result = BudgetCode.Create(
            TestBudgeting.TenantId,
            "ZBB-CREW-01",
            TestBudgeting.CodeDetails(reviewFrequency: (BudgetReviewFrequency)99),
            actorId: null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.ReviewFrequencyInvalid, result.Error);
    }

    [Fact]
    public void An_out_of_range_service_line_is_rejected()
    {
        var result = BudgetCode.Create(
            TestBudgeting.TenantId,
            "ZBB-CREW-01",
            TestBudgeting.CodeDetails(serviceLine: (BudgetServiceLine)99),
            actorId: null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.ServiceLineInvalid, result.Error);
    }

    [Fact]
    public void An_out_of_range_tax_treatment_is_rejected()
    {
        var result = BudgetCode.Create(
            TestBudgeting.TenantId,
            "ZBB-CREW-01",
            TestBudgeting.CodeDetails(taxTreatment: (BudgetTaxTreatment)99),
            actorId: null);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.TaxTreatmentInvalid, result.Error);
    }

    [Fact]
    public void Update_rewrites_the_details_but_never_the_code()
    {
        var code = TestBudgeting.CreateCode("ZBB-CREW-01");

        var result = code.Update(
            TestBudgeting.CodeDetails(
                name: "Crew shuttle charter revenue",
                category: BudgetCodeCategory.Expense,
                serviceLine: BudgetServiceLine.Administrative,
                description: "Reworded after the Q4 review."),
            TestBudgeting.ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("ZBB-CREW-01", code.Code);
        Assert.Equal("Crew shuttle charter revenue", code.Name);
        Assert.Equal(BudgetCodeCategory.Expense, code.Category);
        Assert.Equal(BudgetServiceLine.Administrative, code.ServiceLine);
        Assert.Single(code.DomainEvents.OfType<BudgetCodeUpdatedDomainEvent>());
    }

    [Fact]
    public void Update_rejects_invalid_details_and_leaves_the_code_untouched()
    {
        var code = TestBudgeting.CreateCode();

        var result = code.Update(TestBudgeting.CodeDetails(name: ""), TestBudgeting.ActorId);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.NameRequired, result.Error);
        Assert.Equal("Alamos crew shuttle", code.Name);
        Assert.Empty(code.DomainEvents.OfType<BudgetCodeUpdatedDomainEvent>());
    }

    // --- Audit stamps -------------------------------------------------------------------------

    [Fact]
    public void Create_stamps_created_by_and_leaves_modified_by_null()
    {
        var code = TestBudgeting.CreateCode(actorId: TestBudgeting.ActorId);

        Assert.Equal(TestBudgeting.ActorId, code.CreatedBy);
        Assert.Null(code.ModifiedBy);
    }

    [Fact]
    public void Update_stamps_modified_by_and_never_rewrites_created_by()
    {
        var creator = Guid.NewGuid();
        var editor = Guid.NewGuid();
        var code = TestBudgeting.CreateCode(actorId: creator);

        code.Update(TestBudgeting.CodeDetails(name: "Renamed"), editor);

        Assert.Equal(creator, code.CreatedBy);
        Assert.Equal(editor, code.ModifiedBy);
    }

    [Fact]
    public void SetActive_stamps_modified_by()
    {
        var code = TestBudgeting.CreateCode(actorId: Guid.NewGuid());

        code.SetActive(false, TestBudgeting.ActorId);

        Assert.Equal(TestBudgeting.ActorId, code.ModifiedBy);
    }

    [Fact]
    public void A_null_actor_is_accepted_for_work_with_no_authenticated_user()
    {
        var code = TestBudgeting.CreateCode(actorId: null);

        Assert.Null(code.CreatedBy);
    }

    [Fact]
    public void Every_domain_event_carries_the_actor()
    {
        // The audit pipeline serializes events into event_journal.payload, so this is what makes
        // payload->>'actorId' answer "who did this" before the platform-wide actor column lands.
        var code = TestBudgeting.CreateCode(actorId: TestBudgeting.ActorId);
        code.Update(TestBudgeting.CodeDetails(name: "Renamed"), TestBudgeting.ActorId);
        code.SetActive(false, TestBudgeting.ActorId);

        Assert.Equal(TestBudgeting.ActorId, Assert.Single(code.DomainEvents.OfType<BudgetCodeCreatedDomainEvent>()).ActorId);
        Assert.Equal(TestBudgeting.ActorId, Assert.Single(code.DomainEvents.OfType<BudgetCodeUpdatedDomainEvent>()).ActorId);
        Assert.Equal(TestBudgeting.ActorId, Assert.Single(code.DomainEvents.OfType<BudgetCodeActivationChangedDomainEvent>()).ActorId);
    }

    // --- Retirement ---------------------------------------------------------------------------

    [Fact]
    public void SetActive_false_retires_the_code_and_raises_the_event()
    {
        var code = TestBudgeting.CreateCode();

        var result = code.SetActive(false, TestBudgeting.ActorId);

        Assert.True(result.IsSuccess);
        Assert.False(code.IsActive);
        var changed = Assert.Single(code.DomainEvents.OfType<BudgetCodeActivationChangedDomainEvent>());
        Assert.False(changed.IsActive);
    }

    [Fact]
    public void SetActive_to_the_state_it_is_already_in_succeeds_and_raises_nothing()
    {
        // An unchanged aggregate must raise no event: ModuleDbContext rejects a Modified
        // aggregate with an empty event list, and a repeated click is not a modification.
        var code = TestBudgeting.CreateCode();

        var result = code.SetActive(true, TestBudgeting.ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(code.IsActive);
        Assert.Empty(code.DomainEvents.OfType<BudgetCodeActivationChangedDomainEvent>());
    }

    [Fact]
    public void A_retired_code_can_be_brought_back()
    {
        var code = TestBudgeting.CreateCode();
        code.SetActive(false, TestBudgeting.ActorId);

        var result = code.SetActive(true, TestBudgeting.ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(code.IsActive);
        Assert.Equal(2, code.DomainEvents.OfType<BudgetCodeActivationChangedDomainEvent>().Count());
    }
}
