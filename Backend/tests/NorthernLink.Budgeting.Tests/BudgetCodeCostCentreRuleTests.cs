using NorthernLink.Budgeting.Application.Codes.Create;
using NorthernLink.Budgeting.Application.Codes.Update;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// A budget code's cost centre is validated against the tenant's register on create and edit
/// (<c>BudgetCodeCostCentreRule</c>), with the "unchanged is allowed" leniency for a value whose
/// entry has since been retired.
/// </summary>
public class BudgetCodeCostCentreRuleTests
{
    private readonly InMemoryBudgetCodeRepository _codes = new();
    private readonly InMemoryBudgetPeriodRepository _periods = new();
    private readonly InMemoryUserLookupRepository _users = new();
    private readonly InMemoryCostCentreRepository _costCentres = new();
    private readonly BudgetPeriod _period = TestBudgeting.PeriodIn(PeriodState.Draft);
    private readonly CreateBudgetCodeCommandHandler _create;
    private readonly UpdateBudgetCodeCommandHandler _update;

    public BudgetCodeCostCentreRuleTests()
    {
        _periods.Add(_period);
        _create = new CreateBudgetCodeCommandHandler(_codes, _periods, _users, _costCentres);
        _update = new UpdateBudgetCodeCommandHandler(_codes, _periods, _users, _costCentres);
    }

    private static BudgetCodeDetails Expense(string? costCentre) =>
        TestBudgeting.CodeDetails(category: BudgetCodeCategory.Expense, serviceLine: null, costCentre: costCentre);

    private Task<Result<Guid>> CreateAsync(BudgetCodeDetails details, string code = "ZBB-FUEL-01") =>
        _create.Handle(
            new CreateBudgetCodeCommand(TestBudgeting.TenantId, _period.Id, code, details, TestBudgeting.ActorId),
            CancellationToken.None);

    private Task<Result> UpdateAsync(Guid codeId, BudgetCodeDetails details) =>
        _update.Handle(
            new UpdateBudgetCodeCommand(TestBudgeting.TenantId, _period.Id, codeId, details, TestBudgeting.ActorId),
            CancellationToken.None);

    private BudgetCode AddCode(string? costCentre)
    {
        var code = TestBudgeting.CreateCode("ZBB-FUEL-01", Expense(costCentre), periodId: _period.Id);
        _codes.Add(code);
        return code;
    }

    // --- Create ---------------------------------------------------------------------------------

    [Fact]
    public async Task A_registered_active_cost_centre_is_accepted_and_stored_trimmed()
    {
        _costCentres.Add(TestBudgeting.CreateCostCentre("THOMPSON"));

        var result = await CreateAsync(Expense("  THOMPSON "));

        Assert.True(result.IsSuccess);
        Assert.Equal("THOMPSON", Assert.Single(_codes.Codes).CostCentre);
    }

    [Fact]
    public async Task An_unknown_cost_centre_is_a_400_not_found_and_nothing_is_saved()
    {
        var result = await CreateAsync(Expense("NOWHERE"));

        Assert.Equal(BudgetCodeErrors.CostCentreNotFound, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Empty(_codes.Codes);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task Register_matching_is_ordinal()
    {
        _costCentres.Add(TestBudgeting.CreateCostCentre("THOMPSON"));

        Assert.Equal(BudgetCodeErrors.CostCentreNotFound, (await CreateAsync(Expense("thompson"))).Error);
    }

    [Fact]
    public async Task Another_tenants_register_entry_does_not_count()
    {
        _costCentres.Add(TestBudgeting.CreateCostCentre("THOMPSON", tenantId: Guid.NewGuid()));

        Assert.Equal(BudgetCodeErrors.CostCentreNotFound, (await CreateAsync(Expense("THOMPSON"))).Error);
    }

    [Fact]
    public async Task A_retired_cost_centre_is_a_409_on_create()
    {
        _costCentres.Add(TestBudgeting.CreateCostCentre("THOMPSON", active: false));

        var result = await CreateAsync(Expense("THOMPSON"));

        Assert.Equal(BudgetCodeErrors.CostCentreRetired, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task No_cost_centre_needs_no_register(string? costCentre)
    {
        Assert.True((await CreateAsync(Expense(costCentre))).IsSuccess);
    }

    [Fact]
    public async Task The_revenue_rule_is_reported_as_itself_not_as_a_register_miss()
    {
        var result = await CreateAsync(
            TestBudgeting.CodeDetails(category: BudgetCodeCategory.Revenue, costCentre: "NOWHERE"));

        Assert.Equal(BudgetCodeErrors.CostCentreNotAllowedForRevenue, result.Error);
    }

    [Fact]
    public async Task An_over_long_value_is_reported_as_too_long_not_as_a_register_miss()
    {
        var result = await CreateAsync(Expense(new string('X', BudgetCode.CostCentreMaxLength + 1)));

        Assert.Equal(BudgetCodeErrors.CostCentreTooLong, result.Error);
    }

    // --- Update ---------------------------------------------------------------------------------

    [Fact]
    public async Task An_unchanged_cost_centre_whose_entry_was_retired_is_still_accepted()
    {
        // Retiring an entry must not freeze the codes already carrying it.
        _costCentres.Add(TestBudgeting.CreateCostCentre("THOMPSON", active: false));
        var code = AddCode("THOMPSON");

        var result = await UpdateAsync(code.Id, Expense("THOMPSON") with { Name = "Fuel, renamed" });

        Assert.True(result.IsSuccess);
        Assert.Equal("Fuel, renamed", code.Name);
        Assert.Equal("THOMPSON", code.CostCentre);
    }

    [Fact]
    public async Task An_unchanged_value_with_no_register_entry_at_all_is_still_accepted()
    {
        // Pre-register rows are backfilled by AddCostCentres; even if one slipped through, an edit
        // that leaves the value alone must not be the thing that breaks.
        var code = AddCode("LEGACY");

        Assert.True((await UpdateAsync(code.Id, Expense("LEGACY"))).IsSuccess);
    }

    [Fact]
    public async Task Changing_to_a_retired_cost_centre_is_a_409()
    {
        _costCentres.Add(TestBudgeting.CreateCostCentre("THOMPSON"));
        _costCentres.Add(TestBudgeting.CreateCostCentre("CHURCHILL", active: false));
        var code = AddCode("THOMPSON");

        var result = await UpdateAsync(code.Id, Expense("CHURCHILL"));

        Assert.Equal(BudgetCodeErrors.CostCentreRetired, result.Error);
        Assert.Equal("THOMPSON", code.CostCentre);
    }

    [Fact]
    public async Task Changing_to_an_unknown_cost_centre_is_a_400()
    {
        var code = AddCode(null);

        Assert.Equal(BudgetCodeErrors.CostCentreNotFound, (await UpdateAsync(code.Id, Expense("NOWHERE"))).Error);
        Assert.Null(code.CostCentre);
    }

    [Fact]
    public async Task Changing_to_an_active_registered_cost_centre_succeeds_and_clearing_needs_no_check()
    {
        _costCentres.Add(TestBudgeting.CreateCostCentre("CHURCHILL"));
        var code = AddCode(null);

        Assert.True((await UpdateAsync(code.Id, Expense("CHURCHILL"))).IsSuccess);
        Assert.Equal("CHURCHILL", code.CostCentre);

        Assert.True((await UpdateAsync(code.Id, Expense(null))).IsSuccess);
        Assert.Null(code.CostCentre);
    }
}
