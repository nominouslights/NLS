using NorthernLink.Budgeting.Domain.Periods;
using NorthernLink.Budgeting.Domain.Periods.Events;
using Xunit;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// BudgetPeriod factory validation, date/label derivation, overlap semantics, and the forward-only
/// lifecycle (Draft → Finalized → Open → InReview → Closed).
/// </summary>
public class BudgetPeriodTests
{
    /// <summary>The refusal each transition reports when the period is not in its source state.</summary>
    private static NorthernLink.Shared.Kernel.Error RefusalFor(PeriodTransition transition) => transition switch
    {
        PeriodTransition.Finalize => BudgetPeriodErrors.NotDraft,
        PeriodTransition.Open => BudgetPeriodErrors.NotFinalized,
        PeriodTransition.BeginReview => BudgetPeriodErrors.NotOpen,
        PeriodTransition.Close => BudgetPeriodErrors.NotInReview,
        _ => throw new ArgumentOutOfRangeException(nameof(transition), transition, null),
    };

    [Fact]
    public void Create_always_starts_in_Draft()
    {
        var result = BudgetPeriod.Create(TestBudgeting.TenantId, PeriodGranularity.Quarter, 2026, 4);

        Assert.True(result.IsSuccess);
        Assert.Equal(PeriodState.Draft, result.Value.State);
    }

    [Fact]
    public void Quarter_derives_inclusive_dates_and_fiscal_label()
    {
        var result = BudgetPeriod.Create(TestBudgeting.TenantId, PeriodGranularity.Quarter, 2026, 4);

        Assert.True(result.IsSuccess);
        var period = result.Value;
        Assert.Equal(new DateOnly(2026, 10, 1), period.StartsOn);
        Assert.Equal(new DateOnly(2026, 12, 31), period.EndsOn);
        Assert.Equal("FY2026 Q4", period.Label);
    }

    [Fact]
    public void Month_derives_inclusive_dates_and_month_name_label()
    {
        var result = BudgetPeriod.Create(TestBudgeting.TenantId, PeriodGranularity.Month, 2026, 3);

        Assert.True(result.IsSuccess);
        var period = result.Value;
        Assert.Equal(new DateOnly(2026, 3, 1), period.StartsOn);
        Assert.Equal(new DateOnly(2026, 3, 31), period.EndsOn);
        Assert.Equal("March 2026", period.Label);
    }

    [Fact]
    public void February_in_a_leap_year_ends_on_the_29th()
    {
        var result = BudgetPeriod.Create(TestBudgeting.TenantId, PeriodGranularity.Month, 2028, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2028, 2, 29), result.Value.EndsOn);
    }

    [Theory]
    [InlineData(2019)]
    [InlineData(2101)]
    public void Year_outside_2020_to_2100_is_rejected(int year)
    {
        var result = BudgetPeriod.Create(TestBudgeting.TenantId, PeriodGranularity.Quarter, year, 1);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetPeriodErrors.YearOutOfRange, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Quarter_ordinal_outside_1_to_4_is_rejected(int ordinal)
    {
        var result = BudgetPeriod.Create(TestBudgeting.TenantId, PeriodGranularity.Quarter, 2026, ordinal);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetPeriodErrors.OrdinalOutOfRange, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Month_ordinal_outside_1_to_12_is_rejected(int ordinal)
    {
        var result = BudgetPeriod.Create(TestBudgeting.TenantId, PeriodGranularity.Month, 2026, ordinal);

        Assert.True(result.IsFailure);
        Assert.Equal(BudgetPeriodErrors.OrdinalOutOfRange, result.Error);
    }

    [Fact]
    public void Create_raises_the_created_event()
    {
        var period = TestBudgeting.CreatePeriod();

        var created = Assert.Single(period.DomainEvents.OfType<BudgetPeriodCreatedDomainEvent>());
        Assert.Equal(period.Id, created.PeriodId);
        Assert.Equal(TestBudgeting.TenantId, created.TenantId);
    }

    [Fact]
    public void Overlaps_counts_a_same_day_boundary()
    {
        // Q4 2026 starts Oct 1 — a range ending exactly Oct 1 overlaps.
        var period = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2026, 4);

        Assert.True(period.Overlaps(new DateOnly(2026, 7, 1), new DateOnly(2026, 10, 1)));
        Assert.False(period.Overlaps(new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void A_month_inside_an_existing_quarter_overlaps()
    {
        var quarter = TestBudgeting.CreatePeriod(PeriodGranularity.Quarter, 2026, 4);
        var month = TestBudgeting.CreatePeriod(PeriodGranularity.Month, 2026, 11);

        Assert.True(quarter.Overlaps(month.StartsOn, month.EndsOn));
    }

    // --- Lifecycle ---

    [Theory]
    [InlineData(PeriodState.Draft, PeriodTransition.Finalize, PeriodState.Finalized)]
    [InlineData(PeriodState.Finalized, PeriodTransition.Open, PeriodState.Open)]
    [InlineData(PeriodState.Open, PeriodTransition.BeginReview, PeriodState.InReview)]
    [InlineData(PeriodState.InReview, PeriodTransition.Close, PeriodState.Closed)]
    public void Each_transition_moves_one_step_forward_and_journals_both_ends(
        PeriodState from, PeriodTransition transition, PeriodState to)
    {
        var period = TestBudgeting.PeriodIn(from);
        var updatedBefore = period.UpdatedAtUtc;

        var result = period.Transition(transition, TestBudgeting.ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(to, period.State);
        Assert.True(period.UpdatedAtUtc >= updatedBefore);

        var changed = Assert.Single(period.DomainEvents.OfType<BudgetPeriodStateChangedDomainEvent>());
        Assert.Equal(period.Id, changed.PeriodId);
        Assert.Equal(TestBudgeting.TenantId, changed.TenantId);
        Assert.Equal(from, changed.From);
        Assert.Equal(to, changed.To);
        Assert.Equal(TestBudgeting.ActorId, changed.ActorId);
    }

    [Fact]
    public void The_full_walk_ends_Closed_with_one_event_per_step()
    {
        var period = TestBudgeting.CreatePeriod();

        foreach (var transition in TestBudgeting.TransitionsInOrder)
        {
            Assert.True(period.Transition(transition, actorId: null).IsSuccess);
        }

        Assert.Equal(PeriodState.Closed, period.State);
        var steps = period.DomainEvents.OfType<BudgetPeriodStateChangedDomainEvent>().ToList();
        Assert.Equal(
            [PeriodState.Draft, PeriodState.Finalized, PeriodState.Open, PeriodState.InReview],
            steps.Select(s => s.From).ToArray());
        Assert.Equal(
            [PeriodState.Finalized, PeriodState.Open, PeriodState.InReview, PeriodState.Closed],
            steps.Select(s => s.To).ToArray());
        Assert.All(steps, s => Assert.Null(s.ActorId));
    }

    // Every (state, transition) pair that is not one of the four edges above. Forward-only means
    // a skipped step (Draft → Open), a repeated step (Finalized → Finalize) and any step out of
    // Closed all fail, each with the error of the transition that was attempted.
    [Theory]
    [InlineData(PeriodState.Draft, PeriodTransition.Open)]
    [InlineData(PeriodState.Draft, PeriodTransition.BeginReview)]
    [InlineData(PeriodState.Draft, PeriodTransition.Close)]
    [InlineData(PeriodState.Finalized, PeriodTransition.Finalize)]
    [InlineData(PeriodState.Finalized, PeriodTransition.BeginReview)]
    [InlineData(PeriodState.Finalized, PeriodTransition.Close)]
    [InlineData(PeriodState.Open, PeriodTransition.Finalize)]
    [InlineData(PeriodState.Open, PeriodTransition.Open)]
    [InlineData(PeriodState.Open, PeriodTransition.Close)]
    [InlineData(PeriodState.InReview, PeriodTransition.Finalize)]
    [InlineData(PeriodState.InReview, PeriodTransition.Open)]
    [InlineData(PeriodState.InReview, PeriodTransition.BeginReview)]
    [InlineData(PeriodState.Closed, PeriodTransition.Finalize)]
    [InlineData(PeriodState.Closed, PeriodTransition.Open)]
    [InlineData(PeriodState.Closed, PeriodTransition.BeginReview)]
    [InlineData(PeriodState.Closed, PeriodTransition.Close)]
    public void A_transition_from_the_wrong_state_is_refused_with_that_transitions_error(
        PeriodState state, PeriodTransition transition)
    {
        var period = TestBudgeting.PeriodIn(state);
        var updatedBefore = period.UpdatedAtUtc;

        var result = period.Transition(transition, TestBudgeting.ActorId);

        Assert.True(result.IsFailure);
        Assert.Equal(RefusalFor(transition), result.Error);
        Assert.Equal(state, period.State);
        Assert.Equal(updatedBefore, period.UpdatedAtUtc);
        Assert.Empty(period.DomainEvents);
    }

    [Fact]
    public void The_invalid_pair_table_covers_every_pair_that_is_not_an_edge()
    {
        // Guards the table above against a fifth state or transition arriving without a row:
        // 5 states × 4 transitions = 20 pairs, 4 of them valid, so 16 must refuse.
        var refused = 0;
        foreach (var state in Enum.GetValues<PeriodState>())
        {
            foreach (var transition in Enum.GetValues<PeriodTransition>())
            {
                if (TestBudgeting.PeriodIn(state).Transition(transition, null).IsFailure)
                {
                    refused++;
                }
            }
        }

        Assert.Equal(16, refused);
    }

    [Fact]
    public void An_unknown_transition_value_throws_rather_than_silently_refusing()
    {
        // A cast-in enum value is a programming error, not a business refusal: it must not
        // come back as a 409 the caller could misread as "wrong state".
        var period = TestBudgeting.CreatePeriod();

        Assert.Throws<ArgumentOutOfRangeException>(() => period.Transition((PeriodTransition)99, null));
        Assert.Equal(PeriodState.Draft, period.State);
    }

    [Theory]
    [InlineData(PeriodState.Draft, true)]
    [InlineData(PeriodState.Finalized, false)]
    [InlineData(PeriodState.Open, true)]
    [InlineData(PeriodState.InReview, false)]
    [InlineData(PeriodState.Closed, false)]
    public void The_plan_is_editable_in_Draft_and_Open_only(PeriodState state, bool editable)
    {
        Assert.Equal(editable, TestBudgeting.PeriodIn(state).AllowsPlanChanges);
    }
}
