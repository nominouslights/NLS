using NorthernLink.Budgeting.Application.Actuals.Delete;
using NorthernLink.Budgeting.Domain.Actuals;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// DeleteActualTransactionCommandHandler: one guard — the row must exist. No period-state guard
/// and no retired-code guard, both deliberately.
/// </summary>
public class DeleteActualTransactionCommandHandlerTests
{
    private readonly InMemoryActualTransactionRepository _transactions = new();
    private readonly DeleteActualTransactionCommandHandler _handler;

    private readonly BudgetCode _code = TestBudgeting.CreateCode("ZBB-FUEL-01");

    public DeleteActualTransactionCommandHandlerTests()
    {
        _handler = new DeleteActualTransactionCommandHandler(_transactions);
    }

    private Task<Result> DeleteAsync(Guid transactionId) =>
        _handler.Handle(
            new DeleteActualTransactionCommand(TestBudgeting.TenantId, transactionId), CancellationToken.None);

    private ActualTransaction AddTransaction(string code = "ZBB-FUEL-01")
    {
        var transaction = TestBudgeting.CreateActual(_code.Id, code);
        _transactions.Add(transaction);
        return transaction;
    }

    [Fact]
    public async Task The_transaction_is_removed_and_saved_once_leaving_the_others_alone()
    {
        var duplicate = AddTransaction();
        var keeper = AddTransaction("ZBB-MAINT-01");

        var result = await DeleteAsync(duplicate.Id);

        Assert.True(result.IsSuccess);
        Assert.Same(keeper, Assert.Single(_transactions.Transactions));
        Assert.Equal(1, _transactions.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_unknown_transaction_is_not_found_and_nothing_is_saved()
    {
        AddTransaction();

        var result = await DeleteAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(ActualTransactionErrors.NotFound, result.Error);
        Assert.Single(_transactions.Transactions);
        Assert.Equal(0, _transactions.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_transaction_on_a_code_retired_since_can_still_be_removed()
    {
        // Deliberate: removal is not gated on IsActive. Recording new spend against a retired
        // code is refused; clearing a bad row off it is the clean-up that has to stay possible.
        var transaction = AddTransaction();
        Assert.True(_code.SetActive(false, TestBudgeting.ActorId).IsSuccess);

        var result = await DeleteAsync(transaction.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(_transactions.Transactions);
    }

    [Fact]
    public async Task The_handler_never_asks_about_a_period_at_all()
    {
        // It takes no period repository: there is no state a period could be in that stops a
        // duplicate coming out of the books. Only the plan is frozen by the lifecycle.
        var transaction = AddTransaction();

        Assert.DoesNotContain(
            typeof(DeleteActualTransactionCommandHandler).GetConstructors().Single().GetParameters(),
            p => p.ParameterType.Name.Contains("Period", StringComparison.Ordinal));

        Assert.True((await DeleteAsync(transaction.Id)).IsSuccess);
    }
}
