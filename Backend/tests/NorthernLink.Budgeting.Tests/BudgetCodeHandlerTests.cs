using NorthernLink.Budgeting.Application.Codes.Create;
using NorthernLink.Budgeting.Application.Codes.SetActive;
using NorthernLink.Budgeting.Application.Codes.Update;
using NorthernLink.Budgeting.Application.Integration;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>The budget-code write handlers: uniqueness, owner and parent rules, persistence.</summary>
public class BudgetCodeHandlerTests
{
    private readonly InMemoryBudgetCodeRepository _repository = new();
    private readonly InMemoryUserLookupRepository _users = new();
    private readonly CreateBudgetCodeCommandHandler _create;
    private readonly UpdateBudgetCodeCommandHandler _update;
    private readonly SetBudgetCodeActiveCommandHandler _setActive;

    public BudgetCodeHandlerTests()
    {
        _create = new CreateBudgetCodeCommandHandler(_repository, _users);
        _update = new UpdateBudgetCodeCommandHandler(_repository, _users);
        _setActive = new SetBudgetCodeActiveCommandHandler(_repository);
    }

    private Guid AddUser(string email = "planner@northernlink.ca")
    {
        var userId = Guid.NewGuid();
        _users.Users.Add(new UserLookup
        {
            UserId = userId,
            TenantId = TestBudgeting.TenantId,
            Email = email,
            Role = Roles.Accountant,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        return userId;
    }

    private Task<Result<Guid>> CreateAsync(string code, BudgetCodeDetails? details = null) =>
        _create.Handle(
            new CreateBudgetCodeCommand(
                TestBudgeting.TenantId, code, details ?? TestBudgeting.CodeDetails(), TestBudgeting.ActorId),
            CancellationToken.None);

    // --- Creation and uniqueness ----------------------------------------------------------------

    [Fact]
    public async Task Creates_a_code_and_saves_once()
    {
        var result = await CreateAsync("ZBB-CREW-01");

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(_repository.Codes);
        Assert.Equal(stored.Id, result.Value);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_created_code_records_the_actor_from_the_command_not_the_payload()
    {
        await CreateAsync("ZBB-CREW-01");

        Assert.Equal(TestBudgeting.ActorId, Assert.Single(_repository.Codes).CreatedBy);
    }

    [Fact]
    public async Task A_duplicate_code_is_rejected()
    {
        _repository.Add(TestBudgeting.CreateCode("ZBB-CREW-01"));

        var result = await CreateAsync("ZBB-CREW-01");

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.DuplicateCode, result.Error);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_code_differing_only_in_case_or_whitespace_is_a_duplicate()
    {
        // Two codes a reader could not tell apart on a report are the same code.
        _repository.Add(TestBudgeting.CreateCode("ZBB-CREW-01"));

        var result = await CreateAsync("  zbb-crew-01 ");

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.DuplicateCode, result.Error);
    }

    [Fact]
    public async Task Invalid_details_report_validation_not_conflict()
    {
        // Domain validation runs before every cross-row lookup, so a bad payload never reaches
        // the duplicate, parent or owner checks.
        _repository.Add(TestBudgeting.CreateCode("ZBB-CREW-01"));

        var result = await CreateAsync("ZBB-CREW-01", TestBudgeting.CodeDetails(name: ""));

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.NameRequired, result.Error);
    }

    [Fact]
    public async Task A_different_code_succeeds_alongside_an_existing_one()
    {
        _repository.Add(TestBudgeting.CreateCode("ZBB-CREW-01"));

        var result = await CreateAsync("ZBB-FUEL-01");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _repository.Codes.Count);
    }

    // --- Budget owner ---------------------------------------------------------------------------

    [Fact]
    public async Task A_known_budget_owner_is_accepted()
    {
        var userId = AddUser();

        var result = await CreateAsync("ZBB-CREW-01", TestBudgeting.CodeDetails(budgetOwnerUserId: userId));

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, Assert.Single(_repository.Codes).BudgetOwnerUserId);
    }

    [Fact]
    public async Task An_unknown_budget_owner_is_rejected()
    {
        // This is what makes BudgetOwnerUserId a real reference rather than a free-text name:
        // the replica is tenant-filtered, so another tenant's user is also "unknown" here.
        var result = await CreateAsync(
            "ZBB-CREW-01", TestBudgeting.CodeDetails(budgetOwnerUserId: Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.BudgetOwnerNotFound, result.Error);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_null_budget_owner_is_accepted()
    {
        var result = await CreateAsync("ZBB-CREW-01", TestBudgeting.CodeDetails(budgetOwnerUserId: null));

        Assert.True(result.IsSuccess);
    }

    // --- The one-level hierarchy ------------------------------------------------------------------

    [Fact]
    public async Task A_top_level_parent_is_accepted()
    {
        var parent = TestBudgeting.CreateCode("ZBB-REV");
        _repository.Add(parent);

        var result = await CreateAsync("ZBB-CREW-01", TestBudgeting.CodeDetails(parentCodeId: parent.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(parent.Id, _repository.Codes.Single(c => c.Code == "ZBB-CREW-01").ParentCodeId);
    }

    [Fact]
    public async Task An_unknown_parent_is_rejected()
    {
        var result = await CreateAsync(
            "ZBB-CREW-01", TestBudgeting.CodeDetails(parentCodeId: Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.ParentNotFound, result.Error);
    }

    [Fact]
    public async Task A_parent_that_already_has_a_parent_is_rejected()
    {
        // The hierarchy is one level deep, guarded from above.
        var grandparent = TestBudgeting.CreateCode("ZBB-REV");
        var parent = TestBudgeting.CreateCode(
            "ZBB-REV-SUB", TestBudgeting.CodeDetails(parentCodeId: grandparent.Id));
        _repository.Add(grandparent);
        _repository.Add(parent);

        var result = await CreateAsync("ZBB-CREW-01", TestBudgeting.CodeDetails(parentCodeId: parent.Id));

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.ParentIsNotTopLevel, result.Error);
    }

    [Fact]
    public async Task A_code_cannot_be_its_own_parent()
    {
        var code = TestBudgeting.CreateCode("ZBB-CREW-01");
        _repository.Add(code);

        var result = await _update.Handle(
            new UpdateBudgetCodeCommand(
                TestBudgeting.TenantId, code.Id,
                TestBudgeting.CodeDetails(parentCodeId: code.Id), TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.ParentIsSelf, result.Error);
    }

    [Fact]
    public async Task A_code_that_has_children_cannot_be_given_a_parent()
    {
        // The hierarchy guarded from below. Without this a planner builds two levels bottom-up:
        // give A a child B, then give A a parent.
        var top = TestBudgeting.CreateCode("ZBB-REV");
        var middle = TestBudgeting.CreateCode("ZBB-CREW-01");
        var child = TestBudgeting.CreateCode(
            "ZBB-CREW-02", TestBudgeting.CodeDetails(parentCodeId: middle.Id));
        _repository.Add(top);
        _repository.Add(middle);
        _repository.Add(child);

        var result = await _update.Handle(
            new UpdateBudgetCodeCommand(
                TestBudgeting.TenantId, middle.Id,
                TestBudgeting.CodeDetails(parentCodeId: top.Id), TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.CodeWithChildrenCannotHaveParent, result.Error);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_code_with_children_can_still_be_edited_otherwise()
    {
        // The children rule fires only when an update *sets* a parent. Renaming a parent is
        // ordinary editing and must not be blocked by it.
        var parent = TestBudgeting.CreateCode("ZBB-REV");
        var child = TestBudgeting.CreateCode("ZBB-CREW-01", TestBudgeting.CodeDetails(parentCodeId: parent.Id));
        _repository.Add(parent);
        _repository.Add(child);

        var result = await _update.Handle(
            new UpdateBudgetCodeCommand(
                TestBudgeting.TenantId, parent.Id,
                TestBudgeting.CodeDetails(name: "Revenue rollup"), TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Revenue rollup", parent.Name);
    }

    // --- Update and retirement ---------------------------------------------------------------

    [Fact]
    public async Task Update_applies_the_new_details_and_saves()
    {
        var code = TestBudgeting.CreateCode();
        _repository.Add(code);

        var result = await _update.Handle(
            new UpdateBudgetCodeCommand(
                TestBudgeting.TenantId, code.Id,
                TestBudgeting.CodeDetails(name: "Renamed"), TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", code.Name);
        Assert.Equal(TestBudgeting.ActorId, code.ModifiedBy);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Update_of_an_unknown_id_reports_not_found()
    {
        var result = await _update.Handle(
            new UpdateBudgetCodeCommand(
                TestBudgeting.TenantId, Guid.NewGuid(), TestBudgeting.CodeDetails(), TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Update_with_invalid_details_does_not_save()
    {
        var code = TestBudgeting.CreateCode();
        _repository.Add(code);

        var result = await _update.Handle(
            new UpdateBudgetCodeCommand(
                TestBudgeting.TenantId, code.Id,
                TestBudgeting.CodeDetails(description: new string('x', BudgetCode.DescriptionMaxLength + 1)),
                TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.DescriptionTooLong, result.Error);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Deactivate_retires_the_code_without_removing_it()
    {
        var code = TestBudgeting.CreateCode();
        _repository.Add(code);

        var result = await _setActive.Handle(
            new SetBudgetCodeActiveCommand(TestBudgeting.TenantId, code.Id, false, TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(code.IsActive);
        Assert.Single(_repository.Codes);
    }

    [Fact]
    public async Task Retiring_a_parent_does_not_retire_its_children()
    {
        // No cascade, by design: retirement is a statement about one code's availability, and
        // silently retiring a branch would hide codes the planner never touched.
        var parent = TestBudgeting.CreateCode("ZBB-REV");
        var child = TestBudgeting.CreateCode("ZBB-CREW-01", TestBudgeting.CodeDetails(parentCodeId: parent.Id));
        _repository.Add(parent);
        _repository.Add(child);

        await _setActive.Handle(
            new SetBudgetCodeActiveCommand(TestBudgeting.TenantId, parent.Id, false, TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.False(parent.IsActive);
        Assert.True(child.IsActive);
    }

    [Fact]
    public async Task Deactivating_an_already_inactive_code_still_succeeds()
    {
        var code = TestBudgeting.CreateCode();
        code.SetActive(false, TestBudgeting.ActorId);
        _repository.Add(code);

        var result = await _setActive.Handle(
            new SetBudgetCodeActiveCommand(TestBudgeting.TenantId, code.Id, false, TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(code.IsActive);
    }

    [Fact]
    public async Task SetActive_on_an_unknown_id_reports_not_found()
    {
        var result = await _setActive.Handle(
            new SetBudgetCodeActiveCommand(TestBudgeting.TenantId, Guid.NewGuid(), false, TestBudgeting.ActorId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
    }
}
