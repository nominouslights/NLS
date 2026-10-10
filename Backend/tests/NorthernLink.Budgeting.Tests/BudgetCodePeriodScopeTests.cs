using NorthernLink.Budgeting.Application.Codes.Create;
using NorthernLink.Budgeting.Application.Codes.Delete;
using NorthernLink.Budgeting.Application.Codes.SeedStarterSet;
using NorthernLink.Budgeting.Application.Codes.SetActive;
using NorthernLink.Budgeting.Application.Codes.Update;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// Budget codes belong to a period. Every code command is scoped by (tenant, period): the chart
/// follows the period lifecycle (writes only while Draft or Open), the code string is unique per
/// period rather than per tenant, a parent must be in the same period, and a code id of another
/// period — or another tenant — is not found. Every refusal asserts nothing was saved.
/// </summary>
public class BudgetCodePeriodScopeTests
{
    private readonly InMemoryBudgetCodeRepository _codes = new();
    private readonly InMemoryBudgetPeriodRepository _periods = new();
    private readonly InMemoryUserLookupRepository _users = new();
    private readonly InMemoryCostCentreRepository _costCentres = new();
    private readonly StubBudgetCodeUsageProbe _usageProbe = new();

    private readonly BudgetPeriod _q3 = TestBudgeting.PeriodIn(PeriodState.Draft, PeriodGranularity.Quarter, 2026, 3);
    private readonly BudgetPeriod _q4 = TestBudgeting.PeriodIn(PeriodState.Draft, PeriodGranularity.Quarter, 2026, 4);

    private readonly CreateBudgetCodeCommandHandler _create;
    private readonly UpdateBudgetCodeCommandHandler _update;
    private readonly SetBudgetCodeActiveCommandHandler _setActive;
    private readonly DeleteBudgetCodeCommandHandler _delete;
    private readonly SeedStarterBudgetCodesCommandHandler _seed;

    public BudgetCodePeriodScopeTests()
    {
        _periods.Add(_q3);
        _periods.Add(_q4);
        _create = new CreateBudgetCodeCommandHandler(_codes, _periods, _users, _costCentres);
        _update = new UpdateBudgetCodeCommandHandler(_codes, _periods, _users, _costCentres);
        _setActive = new SetBudgetCodeActiveCommandHandler(_codes, _periods);
        _delete = new DeleteBudgetCodeCommandHandler(_codes, _periods, _usageProbe);
        _seed = new SeedStarterBudgetCodesCommandHandler(_codes, _periods);
    }

    private BudgetCode AddCode(BudgetPeriod period, string code = "FUEL", BudgetCodeDetails? details = null)
    {
        var budgetCode = TestBudgeting.CreateCode(code, details, periodId: period.Id);
        _codes.Add(budgetCode);
        return budgetCode;
    }

    private Task<Result<Guid>> CreateAsync(Guid periodId, string code = "FUEL", BudgetCodeDetails? details = null) =>
        _create.Handle(
            new CreateBudgetCodeCommand(
                TestBudgeting.TenantId, periodId, code, details ?? TestBudgeting.CodeDetails(), TestBudgeting.ActorId),
            CancellationToken.None);

    // --- Uniqueness is per period ---------------------------------------------------------------

    [Fact]
    public async Task The_same_code_string_can_exist_in_two_periods()
    {
        var inQ3 = AddCode(_q3, "FUEL");

        var result = await CreateAsync(_q4.Id, "fuel");

        Assert.True(result.IsSuccess);
        var inQ4 = Assert.Single(_codes.Codes, c => c.PeriodId == _q4.Id);
        Assert.Equal("FUEL", inQ4.Code);
        Assert.NotEqual(inQ3.Id, inQ4.Id);
        Assert.Equal(2, _codes.Codes.Count(c => c.Code == "FUEL"));
    }

    [Fact]
    public async Task A_duplicate_within_one_period_is_still_the_duplicate_error()
    {
        AddCode(_q4, "FUEL");

        var result = await CreateAsync(_q4.Id, "fuel");

        Assert.Equal(BudgetCodeErrors.DuplicateCode, result.Error);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_created_code_belongs_to_the_route_period()
    {
        var result = await CreateAsync(_q4.Id);

        Assert.Equal(_q4.Id, Assert.Single(_codes.Codes, c => c.Id == result.Value).PeriodId);
    }

    // --- Hierarchy stays inside the period ------------------------------------------------------

    [Fact]
    public async Task A_parent_from_another_period_is_refused_on_create()
    {
        var parentInQ3 = AddCode(_q3, "FLEET");

        var result = await CreateAsync(_q4.Id, "FUEL", TestBudgeting.CodeDetails(parentCodeId: parentInQ3.Id));

        Assert.Equal(BudgetCodeErrors.ParentNotFound, result.Error);
        Assert.DoesNotContain(_codes.Codes, c => c.PeriodId == _q4.Id);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_parent_from_another_period_is_refused_on_update()
    {
        var parentInQ3 = AddCode(_q3, "FLEET");
        var fuel = AddCode(_q4, "FUEL");

        var result = await _update.Handle(
            new UpdateBudgetCodeCommand(
                TestBudgeting.TenantId, _q4.Id, fuel.Id,
                TestBudgeting.CodeDetails(parentCodeId: parentInQ3.Id), TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.Equal(BudgetCodeErrors.ParentNotFound, result.Error);
        Assert.Null(fuel.ParentCodeId);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_parent_in_the_same_period_is_accepted()
    {
        var fleet = AddCode(_q4, "FLEET");

        var result = await CreateAsync(_q4.Id, "FUEL", TestBudgeting.CodeDetails(parentCodeId: fleet.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(fleet.Id, Assert.Single(_codes.Codes, c => c.Id == result.Value).ParentCodeId);
    }

    // --- A code id of another period (or tenant) is not found -----------------------------------

    [Fact]
    public async Task Update_through_another_periods_route_is_not_found()
    {
        var fuelInQ3 = AddCode(_q3);

        var result = await _update.Handle(
            new UpdateBudgetCodeCommand(
                TestBudgeting.TenantId, _q4.Id, fuelInQ3.Id, TestBudgeting.CodeDetails(name: "Moved?"), TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
        Assert.NotEqual("Moved?", fuelInQ3.Name);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task Retiring_through_another_periods_route_is_not_found()
    {
        var fuelInQ3 = AddCode(_q3);

        var result = await _setActive.Handle(
            new SetBudgetCodeActiveCommand(TestBudgeting.TenantId, _q4.Id, fuelInQ3.Id, false, TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
        Assert.True(fuelInQ3.IsActive);
    }

    [Fact]
    public async Task Retiring_a_code_in_one_period_leaves_the_same_string_in_another_untouched()
    {
        var fuelInQ3 = AddCode(_q3);
        var fuelInQ4 = AddCode(_q4);

        var result = await _setActive.Handle(
            new SetBudgetCodeActiveCommand(TestBudgeting.TenantId, _q4.Id, fuelInQ4.Id, false, TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(fuelInQ4.IsActive);
        Assert.True(fuelInQ3.IsActive);
    }

    [Fact]
    public async Task Deleting_through_another_periods_route_is_not_found()
    {
        var fuelInQ3 = AddCode(_q3);

        var result = await _delete.Handle(
            new DeleteBudgetCodeCommand(TestBudgeting.TenantId, _q4.Id, fuelInQ3.Id), CancellationToken.None);

        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
        Assert.Contains(fuelInQ3, _codes.Codes);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task Another_tenants_code_in_the_same_period_id_is_not_found()
    {
        // Seed a code, then flip the modelled query filter to another tenant: the real repository
        // (tenant filter + RLS) never returns another tenant's row.
        var fuel = AddCode(_q4);
        _codes.QueryFilterTenantId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        var result = await _update.Handle(
            new UpdateBudgetCodeCommand(
                TestBudgeting.TenantId, _q4.Id, fuel.Id, TestBudgeting.CodeDetails(name: "Theirs?"), TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
        Assert.NotEqual("Theirs?", fuel.Name);
    }

    [Fact]
    public async Task Every_write_to_an_unknown_period_is_period_not_found()
    {
        var unknown = Guid.NewGuid();
        var fuel = AddCode(_q4);

        Assert.Equal(BudgetPeriodErrors.NotFound, (await CreateAsync(unknown)).Error);
        Assert.Equal(BudgetPeriodErrors.NotFound, (await _update.Handle(
            new UpdateBudgetCodeCommand(TestBudgeting.TenantId, unknown, fuel.Id, TestBudgeting.CodeDetails(), null),
            CancellationToken.None)).Error);
        Assert.Equal(BudgetPeriodErrors.NotFound, (await _setActive.Handle(
            new SetBudgetCodeActiveCommand(TestBudgeting.TenantId, unknown, fuel.Id, false, null),
            CancellationToken.None)).Error);
        Assert.Equal(BudgetPeriodErrors.NotFound, (await _delete.Handle(
            new DeleteBudgetCodeCommand(TestBudgeting.TenantId, unknown, fuel.Id), CancellationToken.None)).Error);
        Assert.Equal(BudgetPeriodErrors.NotFound, (await _seed.Handle(
            new SeedStarterBudgetCodesCommand(TestBudgeting.TenantId, unknown, null), CancellationToken.None)).Error);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    // --- The chart follows the period lifecycle -------------------------------------------------

    public static TheoryData<PeriodState> ReadOnlyStates =>
        [PeriodState.Finalized, PeriodState.InReview, PeriodState.Closed];

    private BudgetPeriod ReadOnlyPeriod(PeriodState state)
    {
        var period = TestBudgeting.PeriodIn(state, PeriodGranularity.Month, 2026, 1);
        _periods.Add(period);
        return period;
    }

    [Theory]
    [MemberData(nameof(ReadOnlyStates))]
    public async Task Creating_a_code_is_refused_outside_Draft_and_Open(PeriodState state)
    {
        var period = ReadOnlyPeriod(state);

        var result = await CreateAsync(period.Id);

        Assert.Equal(BudgetCodeErrors.PeriodNotEditable, result.Error);
        Assert.Equal("Budgeting.Code.PeriodNotEditable", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Empty(_codes.Codes);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Theory]
    [MemberData(nameof(ReadOnlyStates))]
    public async Task Editing_a_code_is_refused_outside_Draft_and_Open(PeriodState state)
    {
        var period = ReadOnlyPeriod(state);
        var fuel = AddCode(period);

        var result = await _update.Handle(
            new UpdateBudgetCodeCommand(
                TestBudgeting.TenantId, period.Id, fuel.Id, TestBudgeting.CodeDetails(name: "Late edit"), TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.Equal(BudgetCodeErrors.PeriodNotEditable, result.Error);
        Assert.NotEqual("Late edit", fuel.Name);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Theory]
    [MemberData(nameof(ReadOnlyStates))]
    public async Task Retiring_or_restoring_a_code_is_refused_outside_Draft_and_Open(PeriodState state)
    {
        var period = ReadOnlyPeriod(state);
        var fuel = AddCode(period);

        var retire = await _setActive.Handle(
            new SetBudgetCodeActiveCommand(TestBudgeting.TenantId, period.Id, fuel.Id, false, TestBudgeting.ActorId),
            CancellationToken.None);
        var restore = await _setActive.Handle(
            new SetBudgetCodeActiveCommand(TestBudgeting.TenantId, period.Id, fuel.Id, true, TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.Equal(BudgetCodeErrors.PeriodNotEditable, retire.Error);
        Assert.Equal(BudgetCodeErrors.PeriodNotEditable, restore.Error);
        Assert.True(fuel.IsActive);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Theory]
    [MemberData(nameof(ReadOnlyStates))]
    public async Task Deleting_a_code_is_refused_outside_Draft_and_Open(PeriodState state)
    {
        var period = ReadOnlyPeriod(state);
        var fuel = AddCode(period);

        var result = await _delete.Handle(
            new DeleteBudgetCodeCommand(TestBudgeting.TenantId, period.Id, fuel.Id), CancellationToken.None);

        Assert.Equal(BudgetCodeErrors.PeriodNotEditable, result.Error);
        Assert.Contains(fuel, _codes.Codes);
        Assert.Equal(0, _codes.SaveChangesCallCount);
    }

    [Theory]
    [MemberData(nameof(ReadOnlyStates))]
    public async Task Seeding_the_starter_set_is_refused_outside_Draft_and_Open(PeriodState state)
    {
        var period = ReadOnlyPeriod(state);

        var result = await _seed.Handle(
            new SeedStarterBudgetCodesCommand(TestBudgeting.TenantId, period.Id, TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.Equal(BudgetCodeErrors.PeriodNotEditable, result.Error);
        Assert.Empty(_codes.Codes);
    }

    [Theory]
    [InlineData(PeriodState.Draft)]
    [InlineData(PeriodState.Open)]
    public async Task Draft_and_Open_periods_accept_chart_changes(PeriodState state)
    {
        var period = TestBudgeting.PeriodIn(state, PeriodGranularity.Month, 2026, 2);
        _periods.Add(period);

        var created = await CreateAsync(period.Id);
        var retired = await _setActive.Handle(
            new SetBudgetCodeActiveCommand(TestBudgeting.TenantId, period.Id, created.Value, false, TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.True(retired.IsSuccess);
    }

    [Fact]
    public async Task The_period_guard_runs_before_the_code_lookup()
    {
        // Closed period AND an unknown code: the period the route addresses is the answer.
        var closed = ReadOnlyPeriod(PeriodState.Closed);

        var result = await _update.Handle(
            new UpdateBudgetCodeCommand(TestBudgeting.TenantId, closed.Id, Guid.NewGuid(), TestBudgeting.CodeDetails(), null),
            CancellationToken.None);

        Assert.Equal(BudgetCodeErrors.PeriodNotEditable, result.Error);
    }

    [Fact]
    public async Task Invalid_details_on_create_report_validation_before_the_period_guard()
    {
        var closed = ReadOnlyPeriod(PeriodState.Closed);

        var result = await CreateAsync(closed.Id, "not valid!");

        Assert.Equal(BudgetCodeErrors.CodeInvalidFormat, result.Error);
    }

    // --- The starter set is per period ----------------------------------------------------------

    [Fact]
    public async Task The_starter_set_seeds_each_period_independently()
    {
        var first = await _seed.Handle(
            new SeedStarterBudgetCodesCommand(TestBudgeting.TenantId, _q3.Id, TestBudgeting.ActorId), CancellationToken.None);
        var second = await _seed.Handle(
            new SeedStarterBudgetCodesCommand(TestBudgeting.TenantId, _q4.Id, TestBudgeting.ActorId), CancellationToken.None);
        var again = await _seed.Handle(
            new SeedStarterBudgetCodesCommand(TestBudgeting.TenantId, _q4.Id, TestBudgeting.ActorId), CancellationToken.None);

        Assert.Equal(StarterBudgetCodes.All.Count, first.Value);
        // Q3 having the starter set has no bearing on Q4's chart.
        Assert.Equal(StarterBudgetCodes.All.Count, second.Value);
        // Idempotent per period.
        Assert.Equal(0, again.Value);
        Assert.Equal(StarterBudgetCodes.All.Count, _codes.Codes.Count(c => c.PeriodId == _q3.Id));
        Assert.Equal(StarterBudgetCodes.All.Count, _codes.Codes.Count(c => c.PeriodId == _q4.Id));
    }
}
