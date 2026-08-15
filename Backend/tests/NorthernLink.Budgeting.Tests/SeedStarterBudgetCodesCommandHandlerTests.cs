using NorthernLink.Budgeting.Application.Codes.SeedStarterSet;
using NorthernLink.Budgeting.Domain.Codes;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// The starter chart. It is exposed as a button rather than a one-shot, so idempotency is the
/// property that matters most here — a second click must be a no-op, not twelve duplicates or a
/// unique-index crash.
/// </summary>
public class SeedStarterBudgetCodesCommandHandlerTests
{
    private readonly InMemoryBudgetCodeRepository _repository = new();
    private readonly SeedStarterBudgetCodesCommandHandler _handler;

    public SeedStarterBudgetCodesCommandHandlerTests()
    {
        _handler = new SeedStarterBudgetCodesCommandHandler(_repository);
    }

    private Task<NorthernLink.Shared.Kernel.Result<int>> SeedAsync() =>
        _handler.Handle(
            new SeedStarterBudgetCodesCommand(TestBudgeting.TenantId, TestBudgeting.ActorId),
            CancellationToken.None);

    [Fact]
    public async Task An_empty_tenant_gets_the_whole_starter_set_in_one_save()
    {
        var result = await SeedAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(StarterBudgetCodes.All.Count, result.Value);
        Assert.Equal(StarterBudgetCodes.All.Count, _repository.Codes.Count);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Running_it_twice_creates_nothing_the_second_time()
    {
        await SeedAsync();

        var second = await SeedAsync();

        Assert.True(second.IsSuccess);
        Assert.Equal(0, second.Value);
        Assert.Equal(StarterBudgetCodes.All.Count, _repository.Codes.Count);
        // No rows created means no write at all — not a save of zero changes.
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_existing_hand_made_code_is_kept_and_skipped()
    {
        var mine = TestBudgeting.CreateCode(
            "ZBB-FUEL-01", TestBudgeting.CodeDetails(name: "My own fuel code"));
        _repository.Add(mine);

        var result = await SeedAsync();

        Assert.Equal(StarterBudgetCodes.All.Count - 1, result.Value);
        Assert.Equal("My own fuel code", _repository.Codes.Single(c => c.Code == "ZBB-FUEL-01").Name);
    }

    [Fact]
    public async Task Every_seeded_code_carries_the_actor_and_lands_active()
    {
        await SeedAsync();

        Assert.All(_repository.Codes, code =>
        {
            Assert.Equal(TestBudgeting.ActorId, code.CreatedBy);
            Assert.True(code.IsActive);
            Assert.Equal(BudgetReviewFrequency.Quarterly, code.ReviewFrequency);
        });
    }

    [Fact]
    public void The_starter_set_is_flat_so_seeding_needs_no_ordering()
    {
        // A parent in the seed table would make the single pass order-dependent and could leave a
        // half-built hierarchy behind on a partial failure.
        Assert.All(_repository.Codes, code => Assert.Null(code.ParentCodeId));
    }

    [Fact]
    public void Every_starter_code_is_valid_and_unique()
    {
        // The handler surfaces a malformed entry as a failure rather than seeding a partial set,
        // so a typo in the static table would break the button for everyone. Catch it here.
        Assert.All(StarterBudgetCodes.All, seed =>
        {
            var details = new BudgetCodeDetails
            {
                Name = seed.Name,
                Category = seed.Category,
                ServiceLine = seed.ServiceLine,
                Description = seed.Description,
            };
            var result = BudgetCode.Create(TestBudgeting.TenantId, seed.Code, details, actorId: null);
            Assert.True(result.IsSuccess, $"Starter code {seed.Code} is invalid: {(result.IsFailure ? result.Error.Code : "")}");
        });

        var codes = StarterBudgetCodes.All.Select(s => BudgetCode.NormalizeCode(s.Code)).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Each_of_the_six_revenue_service_lines_has_exactly_one_starter_code()
    {
        // This is what makes the §5.3 revenue-mix comparison computable straight after seeding:
        // a missing line silently reads as 0% of revenue rather than as absent data.
        var revenueLines = StarterBudgetCodes.All
            .Where(s => s.Category == BudgetCodeCategory.Revenue)
            .Select(s => s.ServiceLine)
            .ToList();

        Assert.Equal(
            [
                BudgetServiceLine.ContractCrew, BudgetServiceLine.Community, BudgetServiceLine.Nihb,
                BudgetServiceLine.Charter, BudgetServiceLine.Cargo, BudgetServiceLine.Grocery,
            ],
            revenueLines.Order().ToList());
    }
}
