using NorthernLink.Budgeting.Application.Allocations;
using NorthernLink.Budgeting.Application.Codes.Delete;
using NorthernLink.Budgeting.Domain.Codes;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// AllocationBudgetCodeUsageProbe: a code counts as used when any line in any period carries its
/// id <em>or</em> its string. The second half is the whole reason the probe takes both — a code
/// deleted and recreated under the same string has a new id, and its old lines must still count.
/// The last tests wire the real probe into <see cref="DeleteBudgetCodeCommandHandler"/> so the
/// refusal is proven end to end, not only through <see cref="StubBudgetCodeUsageProbe"/>.
/// </summary>
public class AllocationBudgetCodeUsageProbeTests
{
    private readonly InMemoryBudgetAllocationRepository _allocations = new();
    private readonly AllocationBudgetCodeUsageProbe _probe;

    public AllocationBudgetCodeUsageProbeTests()
    {
        _probe = new AllocationBudgetCodeUsageProbe(_allocations);
    }

    [Fact]
    public async Task With_no_lines_anywhere_nothing_is_referenced()
    {
        Assert.False(await _probe.IsReferencedAsync(Guid.NewGuid(), "ZBB-CREW-01"));
    }

    [Fact]
    public async Task A_line_carrying_the_id_makes_the_code_referenced()
    {
        var codeId = Guid.NewGuid();
        _allocations.Add(TestBudgeting.CreateAllocation(budgetCodeId: codeId, code: "ZBB-CREW-01"));

        // Probed with a string that matches nothing, so only the id can answer.
        Assert.True(await _probe.IsReferencedAsync(codeId, "ZBB-SOMETHING-ELSE"));
    }

    [Fact]
    public async Task A_line_carrying_only_the_string_makes_a_recreated_code_referenced()
    {
        // The line was created against an earlier code row; the code was deleted and recreated
        // under the same string with a fresh id. The old line still pins the string.
        _allocations.Add(TestBudgeting.CreateAllocation(budgetCodeId: Guid.NewGuid(), code: "ZBB-CREW-01"));

        Assert.True(await _probe.IsReferencedAsync(Guid.NewGuid(), "ZBB-CREW-01"));
    }

    [Fact]
    public async Task A_line_matching_neither_the_id_nor_the_string_does_not_count()
    {
        _allocations.Add(TestBudgeting.CreateAllocation(budgetCodeId: Guid.NewGuid(), code: "ZBB-FUEL-01"));

        Assert.False(await _probe.IsReferencedAsync(Guid.NewGuid(), "ZBB-CREW-01"));
    }

    [Fact]
    public async Task The_string_match_is_exact_like_the_database_column()
    {
        // Codes are normalized to upper case on the way in, so a case-different string is a
        // different code; the probe must not be more forgiving than the varchar equality it
        // stands in for, or it would refuse a delete on a code nothing references.
        _allocations.Add(TestBudgeting.CreateAllocation(budgetCodeId: Guid.NewGuid(), code: "ZBB-CREW-01"));

        Assert.False(await _probe.IsReferencedAsync(Guid.NewGuid(), "zbb-crew-01"));
    }

    // --- End to end through the delete handler ---

    [Fact]
    public async Task Deleting_a_code_with_a_line_is_refused_as_in_use_and_the_code_stays()
    {
        var codes = new InMemoryBudgetCodeRepository();
        var code = TestBudgeting.CreateCode("ZBB-CREW-01");
        codes.Add(code);
        _allocations.Add(TestBudgeting.CreateAllocation(budgetCodeId: code.Id, code: code.Code));
        var handler = new DeleteBudgetCodeCommandHandler(codes, _probe);

        var result = await handler.Handle(
            new DeleteBudgetCodeCommand(TestBudgeting.TenantId, code.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.InUse, result.Error);
        Assert.Single(codes.Codes);
        Assert.Equal(0, codes.SaveChangesCallCount);
    }

    [Fact]
    public async Task Deleting_a_recreated_code_whose_old_lines_survive_is_refused_too()
    {
        var codes = new InMemoryBudgetCodeRepository();
        var recreated = TestBudgeting.CreateCode("ZBB-CREW-01");
        codes.Add(recreated);
        _allocations.Add(TestBudgeting.CreateAllocation(budgetCodeId: Guid.NewGuid(), code: "ZBB-CREW-01"));
        var handler = new DeleteBudgetCodeCommandHandler(codes, _probe);

        var result = await handler.Handle(
            new DeleteBudgetCodeCommand(TestBudgeting.TenantId, recreated.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetCodeErrors.InUse, result.Error);
        Assert.Single(codes.Codes);
    }

    [Fact]
    public async Task Deleting_a_code_no_line_references_still_succeeds_with_the_real_probe()
    {
        var codes = new InMemoryBudgetCodeRepository();
        var unused = TestBudgeting.CreateCode("ZBB-UNUSED-01");
        codes.Add(unused);
        _allocations.Add(TestBudgeting.CreateAllocation(budgetCodeId: Guid.NewGuid(), code: "ZBB-CREW-01"));
        var handler = new DeleteBudgetCodeCommandHandler(codes, _probe);

        var result = await handler.Handle(
            new DeleteBudgetCodeCommand(TestBudgeting.TenantId, unused.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(codes.Codes);
        Assert.Equal(1, codes.SaveChangesCallCount);
    }
}
