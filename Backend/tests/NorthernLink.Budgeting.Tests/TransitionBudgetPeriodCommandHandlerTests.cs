using NorthernLink.Budgeting.Application.Periods.Transition;
using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Budgeting.Domain.Periods.Events;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// TransitionBudgetPeriodCommandHandler: load, delegate to the aggregate, save. The full
/// (state, transition) table is the aggregate's and lives in <see cref="BudgetPeriodTests"/>;
/// here the point is that a refusal propagates unchanged and saves nothing.
/// </summary>
public class TransitionBudgetPeriodCommandHandlerTests
{
    private readonly InMemoryBudgetPeriodRepository _repository = new();
    private readonly TransitionBudgetPeriodCommandHandler _handler;

    public TransitionBudgetPeriodCommandHandlerTests()
    {
        _handler = new TransitionBudgetPeriodCommandHandler(_repository);
    }

    private Task<Result> TransitionAsync(Guid periodId, PeriodTransition transition, Guid? actorId = null) =>
        _handler.Handle(
            new TransitionBudgetPeriodCommand(TestBudgeting.TenantId, periodId, transition, actorId ?? TestBudgeting.ActorId),
            CancellationToken.None);

    [Theory]
    [InlineData(PeriodState.Draft, PeriodTransition.Finalize, PeriodState.Finalized)]
    [InlineData(PeriodState.Finalized, PeriodTransition.Open, PeriodState.Open)]
    [InlineData(PeriodState.Open, PeriodTransition.BeginReview, PeriodState.InReview)]
    [InlineData(PeriodState.InReview, PeriodTransition.Close, PeriodState.Closed)]
    public async Task Each_valid_step_changes_the_state_and_saves_once(
        PeriodState from, PeriodTransition transition, PeriodState to)
    {
        var period = TestBudgeting.PeriodIn(from);
        _repository.Add(period);

        var result = await TransitionAsync(period.Id, transition);

        Assert.True(result.IsSuccess);
        Assert.Equal(to, period.State);
        Assert.Equal(1, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task The_actor_from_the_command_rides_the_state_changed_event()
    {
        var period = TestBudgeting.CreatePeriod();
        period.ClearDomainEvents();
        _repository.Add(period);

        await TransitionAsync(period.Id, PeriodTransition.Finalize, TestBudgeting.ActorId);

        var changed = Assert.Single(period.DomainEvents.OfType<BudgetPeriodStateChangedDomainEvent>());
        Assert.Equal(TestBudgeting.ActorId, changed.ActorId);
        Assert.Equal(PeriodState.Draft, changed.From);
        Assert.Equal(PeriodState.Finalized, changed.To);
    }

    [Fact]
    public async Task Walking_all_four_steps_through_the_handler_closes_the_period()
    {
        var period = TestBudgeting.CreatePeriod();
        _repository.Add(period);

        foreach (var transition in TestBudgeting.TransitionsInOrder)
        {
            Assert.True((await TransitionAsync(period.Id, transition)).IsSuccess);
        }

        Assert.Equal(PeriodState.Closed, period.State);
        Assert.Equal(4, _repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task An_unknown_id_reports_not_found_and_saves_nothing()
    {
        _repository.Add(TestBudgeting.CreatePeriod());

        var result = await TransitionAsync(Guid.NewGuid(), PeriodTransition.Finalize);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetPeriodErrors.NotFound, result.Error);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(PeriodState.Draft, PeriodTransition.Open)]
    [InlineData(PeriodState.Draft, PeriodTransition.Close)]
    [InlineData(PeriodState.Finalized, PeriodTransition.Finalize)]
    [InlineData(PeriodState.Open, PeriodTransition.Finalize)]
    [InlineData(PeriodState.InReview, PeriodTransition.BeginReview)]
    [InlineData(PeriodState.Closed, PeriodTransition.Close)]
    public async Task A_step_from_the_wrong_state_reports_that_transitions_error_and_saves_nothing(
        PeriodState state, PeriodTransition transition)
    {
        var period = TestBudgeting.PeriodIn(state);
        _repository.Add(period);
        var expected = transition switch
        {
            PeriodTransition.Finalize => BudgetPeriodErrors.NotDraft,
            PeriodTransition.Open => BudgetPeriodErrors.NotFinalized,
            PeriodTransition.BeginReview => BudgetPeriodErrors.NotOpen,
            PeriodTransition.Close => BudgetPeriodErrors.NotInReview,
            _ => throw new ArgumentOutOfRangeException(nameof(transition)),
        };

        var result = await TransitionAsync(period.Id, transition);

        Assert.True(result.IsFailure);
        Assert.Equal(expected, result.Error);
        Assert.Equal(state, period.State);
        Assert.Empty(period.DomainEvents);
        Assert.Equal(0, _repository.SaveChangesCallCount);
    }
}
