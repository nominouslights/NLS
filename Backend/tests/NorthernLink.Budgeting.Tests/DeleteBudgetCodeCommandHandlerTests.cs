using NorthernLink.Budgeting.Application.Codes.Delete;
using NorthernLink.Budgeting.Domain.Codes;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// The narrow hard-delete path. Every refusal here also asserts <c>SaveChangesCallCount == 0</c>:
/// a guard that returns a failure *after* saving is the classic way this kind of check ships
/// broken, and it would not be visible from the response.
/// </summary>
public class DeleteBudgetCodeCommandHandlerTests
{
    private readonly InMemoryBudgetCodeRepository _repository = new();
    private readonly StubBudgetCodeUsageProbe _usageProbe = new();
    private readonly DeleteBudgetCodeCommandHandler _handler;

    public DeleteBudgetCodeCommandHandlerTests()
    {
        _handler = new DeleteBudgetCodeCommandHandler(_repository, _usageProbe);
    }

    private Task<NorthernLink.Shared.Kernel.Result> DeleteAsync(Guid id) =>
        _handler.Handle(new DeleteBudgetCodeCommand(TestBudgeting.TenantId, id), CancellationToken.None);

    [Fact]
    public async Task An_unused_code_is_deleted_and_saved_once()
    {
        var code = TestBudgeting.CreateCode();
        _repository.Add(code);

        var result = await DeleteAsync(code.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(_repository.Codes);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_unknown_id_reports_not_found()
    {
        var result = await DeleteAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.NotFound, result.Error);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_referenced_code_is_refused_and_left_in_place()
    {
        var code = TestBudgeting.CreateCode();
        _repository.Add(code);
        _usageProbe.Referenced = true;

        var result = await DeleteAsync(code.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.InUse, result.Error);
        Assert.Single(_repository.Codes);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_usage_probe_receives_both_the_id_and_the_code_string()
    {
        // Allocations reference a code by its string; a future table may use the id. The probe
        // takes both so Stage 6.2 swaps the implementation without touching this handler.
        var code = TestBudgeting.CreateCode("ZBB-FUEL-01");
        _repository.Add(code);

        await DeleteAsync(code.Id);

        Assert.Equal(code.Id, _usageProbe.LastProbedId);
        Assert.Equal("ZBB-FUEL-01", _usageProbe.LastProbedCode);
    }

    [Fact]
    public async Task A_code_with_children_is_refused_before_the_usage_probe_runs()
    {
        // There is no database foreign key on parent_code_id, so Postgres will not catch this.
        // A dangling parent id does not throw — it quietly drops a branch from a rollup report.
        var parent = TestBudgeting.CreateCode("ZBB-REV");
        var child = TestBudgeting.CreateCode("ZBB-CREW-01", TestBudgeting.CodeDetails(parentCodeId: parent.Id));
        _repository.Add(parent);
        _repository.Add(child);

        var result = await DeleteAsync(parent.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.ParentHasChildren, result.Error);
        Assert.Equal(2, _repository.Codes.Count);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_child_can_be_deleted_freeing_its_parent()
    {
        var parent = TestBudgeting.CreateCode("ZBB-REV");
        var child = TestBudgeting.CreateCode("ZBB-CREW-01", TestBudgeting.CodeDetails(parentCodeId: parent.Id));
        _repository.Add(parent);
        _repository.Add(child);

        Assert.True((await DeleteAsync(child.Id)).IsSuccess);
        Assert.True((await DeleteAsync(parent.Id)).IsSuccess);
        Assert.Empty(_repository.Codes);
    }

    [Fact]
    public async Task A_retired_code_that_was_never_used_can_still_be_deleted()
    {
        // Retirement and deletion answer different questions. Retiring is about availability for
        // new work; deleting is about a code that should never have existed.
        var code = TestBudgeting.CreateCode();
        code.SetActive(false, TestBudgeting.ActorId);
        _repository.Add(code);

        Assert.True((await DeleteAsync(code.Id)).IsSuccess);
        Assert.Empty(_repository.Codes);
    }
}
