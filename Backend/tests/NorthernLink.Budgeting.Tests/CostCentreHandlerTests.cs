using NorthernLink.Budgeting.Application.CostCentres;
using NorthernLink.Budgeting.Application.CostCentres.Create;
using NorthernLink.Budgeting.Application.CostCentres.Delete;
using NorthernLink.Budgeting.Application.CostCentres.SetActive;
using NorthernLink.Budgeting.Application.CostCentres.Update;
using NorthernLink.Budgeting.Application.Integration;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.CostCentres;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>The cost-centre register's write handlers.</summary>
public class CostCentreHandlerTests
{
    private readonly InMemoryCostCentreRepository _repository = new();
    private readonly InMemoryUserLookupRepository _users = new();
    private readonly InMemoryBudgetCodeRepository _codes = new();
    private readonly CreateCostCentreCommandHandler _create;
    private readonly UpdateCostCentreCommandHandler _update;
    private readonly SetCostCentreActiveCommandHandler _setActive;
    private readonly DeleteCostCentreCommandHandler _delete;

    public CostCentreHandlerTests()
    {
        _create = new CreateCostCentreCommandHandler(_repository, _users);
        _update = new UpdateCostCentreCommandHandler(_repository, _users);
        _setActive = new SetCostCentreActiveCommandHandler(_repository);
        // The real probe over the in-memory code repository, so "in use" is exercised end to end.
        _delete = new DeleteCostCentreCommandHandler(_repository, new BudgetCodeCostCentreUsageProbe(_codes));
    }

    private static CostCentreDetails Details(
        string name = "Thompson base", Guid? ownerUserId = null, Guid? parentId = null) =>
        new() { Name = name, OwnerUserId = ownerUserId, ParentId = parentId };

    private Task<Result<Guid>> CreateAsync(string? code, CostCentreDetails? details = null) =>
        _create.Handle(
            new CreateCostCentreCommand(TestBudgeting.TenantId, code, details ?? Details(), TestBudgeting.ActorId),
            CancellationToken.None);

    private Task<Result> UpdateAsync(Guid id, CostCentreDetails details, string? code = null) =>
        _update.Handle(
            new UpdateCostCentreCommand(TestBudgeting.TenantId, id, code, details, TestBudgeting.ActorId),
            CancellationToken.None);

    private Task<Result> SetActiveAsync(Guid id, bool active) =>
        _setActive.Handle(
            new SetCostCentreActiveCommand(TestBudgeting.TenantId, id, active, TestBudgeting.ActorId),
            CancellationToken.None);

    private Task<Result> DeleteAsync(Guid id) =>
        _delete.Handle(new DeleteCostCentreCommand(TestBudgeting.TenantId, id), CancellationToken.None);

    private CostCentre Add(string code, Guid? parentId = null, bool active = true)
    {
        var costCentre = TestBudgeting.CreateCostCentre(code, code + " unit", parentId: parentId, active: active);
        _repository.Add(costCentre);
        return costCentre;
    }

    private Guid AddUser(Guid? tenantId = null)
    {
        var userId = Guid.NewGuid();
        _users.Users.Add(new UserLookup
        {
            UserId = userId,
            TenantId = tenantId ?? TestBudgeting.TenantId,
            Email = "owner@northernlink.ca",
            Role = Roles.Owner,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        return userId;
    }

    // --- Create ---------------------------------------------------------------------------------

    [Fact]
    public async Task Creates_an_entry_and_saves_once()
    {
        var result = await CreateAsync("THOMPSON");

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(_repository.CostCentres);
        Assert.Equal(result.Value, stored.Id);
        Assert.Equal(TestBudgeting.ActorId, stored.CreatedBy);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_duplicate_code_is_a_conflict_after_trimming()
    {
        Add("THOMPSON");

        var result = await CreateAsync("  THOMPSON ");

        Assert.Equal(CostCentreErrors.DuplicateCode, result.Error);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Codes_differing_only_in_case_are_distinct_entries()
    {
        // Ordinal, matching how budget codes have always stored the string — see CostCentre.
        Add("THOMPSON");

        var result = await CreateAsync("Thompson");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Another_tenants_code_is_not_a_duplicate()
    {
        _repository.Add(TestBudgeting.CreateCostCentre("THOMPSON", tenantId: Guid.NewGuid()));

        Assert.True((await CreateAsync("THOMPSON")).IsSuccess);
    }

    [Fact]
    public async Task Invalid_details_report_validation_before_the_duplicate_check()
    {
        Add("THOMPSON");

        var result = await CreateAsync("THOMPSON", Details(name: ""));

        Assert.Equal(CostCentreErrors.NameRequired, result.Error);
    }

    [Fact]
    public async Task An_owner_must_be_a_user_of_this_tenant()
    {
        var otherTenantsUser = AddUser(Guid.NewGuid());

        Assert.Equal(CostCentreErrors.OwnerNotFound, (await CreateAsync("A", Details(ownerUserId: Guid.NewGuid()))).Error);
        Assert.Equal(CostCentreErrors.OwnerNotFound, (await CreateAsync("B", Details(ownerUserId: otherTenantsUser))).Error);
        Assert.True((await CreateAsync("C", Details(ownerUserId: AddUser()))).IsSuccess);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_parent_must_exist()
    {
        var result = await CreateAsync("CHILD", Details(parentId: Guid.NewGuid()));

        Assert.Equal(CostCentreErrors.ParentNotFound, result.Error);
    }

    [Fact]
    public async Task A_parent_must_be_top_level()
    {
        var top = Add("NORTH");
        var middle = Add("THOMPSON", parentId: top.Id);

        var result = await CreateAsync("YARD", Details(parentId: middle.Id));

        Assert.Equal(CostCentreErrors.ParentIsNotTopLevel, result.Error);
    }

    [Fact]
    public async Task A_parent_must_be_active_when_chosen()
    {
        var retired = Add("NORTH", active: false);

        var result = await CreateAsync("THOMPSON", Details(parentId: retired.Id));

        Assert.Equal(CostCentreErrors.ParentRetired, result.Error);
    }

    [Fact]
    public async Task A_valid_parent_is_recorded()
    {
        var top = Add("NORTH");

        var result = await CreateAsync("THOMPSON", Details(parentId: top.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(top.Id, _repository.CostCentres.Single(c => c.Id == result.Value).ParentId);
    }

    // --- Update ---------------------------------------------------------------------------------

    [Fact]
    public async Task Update_of_an_unknown_id_is_not_found()
    {
        Assert.Equal(CostCentreErrors.NotFound, (await UpdateAsync(Guid.NewGuid(), Details())).Error);
    }

    [Fact]
    public async Task Update_cannot_reach_another_tenants_entry()
    {
        var foreign = TestBudgeting.CreateCostCentre("THOMPSON", tenantId: Guid.NewGuid());
        _repository.Add(foreign);

        Assert.Equal(CostCentreErrors.NotFound, (await UpdateAsync(foreign.Id, Details())).Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("THOMPSON")]
    [InlineData("  THOMPSON  ")]
    public async Task Update_accepts_an_omitted_or_unchanged_code(string? code)
    {
        var costCentre = Add("THOMPSON");

        var result = await UpdateAsync(costCentre.Id, Details(name: "Renamed"), code);

        Assert.True(result.IsSuccess);
        Assert.Equal("THOMPSON", costCentre.Code);
        Assert.Equal("Renamed", costCentre.Name);
    }

    [Theory]
    [InlineData("THOMPSON-2")]
    [InlineData("thompson")]
    public async Task Update_refuses_a_code_change(string code)
    {
        var costCentre = Add("THOMPSON");

        var result = await UpdateAsync(costCentre.Id, Details(name: "Renamed"), code);

        Assert.Equal(CostCentreErrors.CodeImmutable, result.Error);
        Assert.Equal("THOMPSON", costCentre.Code);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Update_validates_the_owner()
    {
        var costCentre = Add("THOMPSON");

        Assert.Equal(
            CostCentreErrors.OwnerNotFound,
            (await UpdateAsync(costCentre.Id, Details(ownerUserId: Guid.NewGuid()))).Error);
    }

    [Fact]
    public async Task A_cost_centre_with_children_cannot_take_a_parent()
    {
        // Guarding from below: without it, A→B then giving A a parent builds two levels.
        var parentCandidate = Add("NORTH");
        var hasChild = Add("THOMPSON");
        Add("YARD", parentId: hasChild.Id);

        var result = await UpdateAsync(hasChild.Id, Details(parentId: parentCandidate.Id));

        Assert.Equal(CostCentreErrors.HasChildrenCannotHaveParent, result.Error);
    }

    [Fact]
    public async Task Update_refuses_self_as_parent()
    {
        var costCentre = Add("THOMPSON");

        Assert.Equal(CostCentreErrors.ParentIsSelf, (await UpdateAsync(costCentre.Id, Details(parentId: costCentre.Id))).Error);
    }

    [Fact]
    public async Task Changing_to_a_retired_parent_is_refused()
    {
        var retired = Add("NORTH", active: false);
        var costCentre = Add("THOMPSON");

        Assert.Equal(CostCentreErrors.ParentRetired, (await UpdateAsync(costCentre.Id, Details(parentId: retired.Id))).Error);
    }

    [Fact]
    public async Task Keeping_a_parent_that_has_since_been_retired_is_allowed()
    {
        var parent = Add("NORTH");
        var child = Add("THOMPSON", parentId: parent.Id);
        parent.SetActive(false, null);

        var result = await UpdateAsync(child.Id, Details(name: "Thompson yard", parentId: parent.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal("Thompson yard", child.Name);
    }

    // --- Activate / deactivate ------------------------------------------------------------------

    [Fact]
    public async Task Deactivating_a_parent_with_active_children_is_refused()
    {
        var parent = Add("NORTH");
        Add("THOMPSON", parentId: parent.Id);

        var result = await SetActiveAsync(parent.Id, false);

        Assert.Equal(CostCentreErrors.HasActiveChildren, result.Error);
        Assert.True(parent.IsActive);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Deactivating_a_parent_whose_children_are_all_retired_succeeds()
    {
        var parent = Add("NORTH");
        Add("THOMPSON", parentId: parent.Id, active: false);

        var result = await SetActiveAsync(parent.Id, false);

        Assert.True(result.IsSuccess);
        Assert.False(parent.IsActive);
    }

    [Fact]
    public async Task Activate_and_deactivate_round_trip_and_unknown_is_not_found()
    {
        var costCentre = Add("THOMPSON");

        Assert.True((await SetActiveAsync(costCentre.Id, false)).IsSuccess);
        Assert.False(costCentre.IsActive);
        Assert.True((await SetActiveAsync(costCentre.Id, true)).IsSuccess);
        Assert.True(costCentre.IsActive);
        Assert.Equal(CostCentreErrors.NotFound, (await SetActiveAsync(Guid.NewGuid(), false)).Error);
    }

    // --- Delete ---------------------------------------------------------------------------------

    [Fact]
    public async Task An_unused_childless_entry_is_deleted()
    {
        var costCentre = Add("THOMPSON");

        var result = await DeleteAsync(costCentre.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(_repository.CostCentres);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_entry_any_budget_code_carries_is_in_use_in_any_period()
    {
        var costCentre = Add("THOMPSON");
        // A code in some other (say, closed) period, retired — it still counts.
        var code = TestBudgeting.CreateCode(
            "ZBB-FUEL-01",
            TestBudgeting.CodeDetails(category: BudgetCodeCategory.Expense, costCentre: "THOMPSON"),
            periodId: Guid.NewGuid());
        code.SetActive(false, null);
        _codes.Add(code);

        var result = await DeleteAsync(costCentre.Id);

        Assert.Equal(CostCentreErrors.InUse, result.Error);
        Assert.Single(_repository.CostCentres);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Usage_matching_is_ordinal()
    {
        var costCentre = Add("THOMPSON");
        _codes.Add(TestBudgeting.CreateCode(
            "ZBB-FUEL-01",
            TestBudgeting.CodeDetails(category: BudgetCodeCategory.Expense, costCentre: "Thompson")));

        Assert.True((await DeleteAsync(costCentre.Id)).IsSuccess);
    }

    [Fact]
    public async Task An_entry_with_children_cannot_be_deleted()
    {
        var parent = Add("NORTH");
        Add("THOMPSON", parentId: parent.Id, active: false);

        Assert.Equal(CostCentreErrors.HasChildren, (await DeleteAsync(parent.Id)).Error);
    }

    [Fact]
    public async Task Deleting_an_unknown_id_is_not_found()
    {
        Assert.Equal(CostCentreErrors.NotFound, (await DeleteAsync(Guid.NewGuid())).Error);
    }
}
