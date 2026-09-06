using NorthernLink.Trips.Domain.Schedules;
using NorthernLink.Trips.Domain.Schedules.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// Domain rules for schedule exceptions (special dates) on <see cref="ScheduleTemplate"/>:
/// the per-kind time rules, one exception per date, update/remove, and the projection's
/// contract that every mutation raises <see cref="ScheduleTemplateExceptionsChangedDomainEvent"/>.
/// </summary>
public class ScheduleExceptionTests
{
    private static readonly DateOnly TreatyDays = new(2026, 8, 14);

    // ----- Skip -----

    [Fact]
    public void Skip_is_added_with_no_times_and_raises_the_exceptions_changed_event()
    {
        var template = TestPlanning.CreateTemplate();
        template.ClearDomainEvents();

        var result = template.AddException(TreatyDays, ScheduleExceptionKind.Skip, null, null, "Treaty Days");

        Assert.True(result.IsSuccess);
        var exception = Assert.Single(template.Exceptions);
        Assert.Equal(result.Value, exception.Id);
        Assert.Equal(TreatyDays, exception.Date);
        Assert.Equal(ScheduleExceptionKind.Skip, exception.Kind);
        Assert.Null(exception.DepartureTime);
        Assert.Null(exception.ReturnDepartureTime);
        Assert.Equal("Treaty Days", exception.Note);
        Assert.Equal(template.TenantId, exception.TenantId);
        Assert.Equal(template.Id, exception.ScheduleTemplateId);
        Assert.Contains(template.DomainEvents, e => e is ScheduleTemplateExceptionsChangedDomainEvent);
    }

    [Fact]
    public void Skip_with_any_time_fails_with_ExceptionTimesNotAllowed()
    {
        var template = TestPlanning.CreateTemplate();

        var withDeparture = template.AddException(
            TreatyDays, ScheduleExceptionKind.Skip, new TimeOnly(6, 30), null, null);
        var withReturn = template.AddException(
            TreatyDays, ScheduleExceptionKind.Skip, null, new TimeOnly(17, 30), null);

        Assert.Equal(ScheduleTemplateErrors.ExceptionTimesNotAllowed, withDeparture.Error);
        Assert.Equal(ScheduleTemplateErrors.ExceptionTimesNotAllowed, withReturn.Error);
        Assert.Empty(template.Exceptions);
    }

    // ----- ExtraRun -----

    [Fact]
    public void ExtraRun_requires_its_own_departure_time()
    {
        var template = TestPlanning.CreateTemplate();

        var result = template.AddException(TreatyDays, ScheduleExceptionKind.ExtraRun, null, null, null);

        Assert.Equal(ScheduleTemplateErrors.ExtraRunDepartureRequired, result.Error);
    }

    [Fact]
    public void ExtraRun_return_at_or_before_departure_fails_even_on_an_overnight_template()
    {
        // The template's own return is overnight, but extra runs are same-day only — the
        // exception's return must still follow its departure on the clock.
        var template = TestPlanning.CreateTemplate(
            departureTime: new TimeOnly(17, 30),
            returnDepartureTime: new TimeOnly(6, 30),
            returnNextDay: true);

        var result = template.AddException(
            TreatyDays, ScheduleExceptionKind.ExtraRun, new TimeOnly(9, 0), new TimeOnly(9, 0), null);

        Assert.Equal(ScheduleTemplateErrors.ExceptionReturnBeforeDeparture, result.Error);
    }

    [Fact]
    public void ExtraRun_with_departure_only_is_valid_on_a_one_way_template()
    {
        var template = TestPlanning.CreateTemplate(returnDepartureTime: null);

        var result = template.AddException(
            TreatyDays, ScheduleExceptionKind.ExtraRun, new TimeOnly(9, 0), null, null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ExtraRun_with_a_same_day_return_pair_is_valid()
    {
        var template = TestPlanning.CreateTemplate(returnDepartureTime: null);

        var result = template.AddException(
            TreatyDays, ScheduleExceptionKind.ExtraRun, new TimeOnly(9, 0), new TimeOnly(15, 0), null);

        Assert.True(result.IsSuccess);
        var exception = Assert.Single(template.Exceptions);
        Assert.Equal(new TimeOnly(9, 0), exception.DepartureTime);
        Assert.Equal(new TimeOnly(15, 0), exception.ReturnDepartureTime);
    }

    // ----- TimeOverride -----

    [Fact]
    public void TimeOverride_with_no_times_fails_with_OverrideTimeRequired()
    {
        var template = TestPlanning.CreateTemplate();

        var result = template.AddException(TreatyDays, ScheduleExceptionKind.TimeOverride, null, null, null);

        Assert.Equal(ScheduleTemplateErrors.OverrideTimeRequired, result.Error);
    }

    [Fact]
    public void TimeOverride_return_on_a_one_way_template_fails()
    {
        var template = TestPlanning.CreateTemplate(returnDepartureTime: null);

        var result = template.AddException(
            TreatyDays, ScheduleExceptionKind.TimeOverride, null, new TimeOnly(18, 0), null);

        Assert.Equal(ScheduleTemplateErrors.ExceptionReturnWithoutTemplateReturn, result.Error);
    }

    [Fact]
    public void TimeOverride_effective_pair_must_order_correctly_with_fallbacks()
    {
        // Template departs 06:30, returns 17:30. Overriding only the departure to 18:00
        // leaves the effective pair (18:00, template's 17:30) inverted.
        var template = TestPlanning.CreateTemplate(
            departureTime: new TimeOnly(6, 30),
            returnDepartureTime: new TimeOnly(17, 30));

        var result = template.AddException(
            TreatyDays, ScheduleExceptionKind.TimeOverride, new TimeOnly(18, 0), null, null);

        Assert.Equal(ScheduleTemplateErrors.ExceptionReturnBeforeDeparture, result.Error);
    }

    [Fact]
    public void TimeOverride_pair_ordering_is_skipped_for_an_overnight_template()
    {
        // ReturnNextDay: an earlier clock time on the return is the whole point.
        var template = TestPlanning.CreateTemplate(
            departureTime: new TimeOnly(17, 30),
            returnDepartureTime: new TimeOnly(6, 30),
            returnNextDay: true);

        var result = template.AddException(
            TreatyDays, ScheduleExceptionKind.TimeOverride, new TimeOnly(19, 0), null, null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TimeOverride_with_one_valid_time_is_accepted()
    {
        var template = TestPlanning.CreateTemplate(
            departureTime: new TimeOnly(6, 30),
            returnDepartureTime: new TimeOnly(17, 30));

        var departureOnly = template.AddException(
            TreatyDays, ScheduleExceptionKind.TimeOverride, new TimeOnly(8, 0), null, null);
        var returnOnly = template.AddException(
            TreatyDays.AddDays(1), ScheduleExceptionKind.TimeOverride, null, new TimeOnly(19, 0), null);

        Assert.True(departureOnly.IsSuccess);
        Assert.True(returnOnly.IsSuccess);
        Assert.Equal(2, template.Exceptions.Count);
    }

    // ----- One per date -----

    [Fact]
    public void Second_exception_on_the_same_date_fails_with_DuplicateExceptionDate()
    {
        var template = TestPlanning.CreateTemplate();
        template.AddException(TreatyDays, ScheduleExceptionKind.Skip, null, null, null);

        var duplicate = template.AddException(
            TreatyDays, ScheduleExceptionKind.ExtraRun, new TimeOnly(9, 0), null, null);

        Assert.Equal(ScheduleTemplateErrors.DuplicateExceptionDate, duplicate.Error);
        Assert.Single(template.Exceptions);
    }

    [Fact]
    public void Past_dates_are_deliberately_allowed()
    {
        // A backdated skip documents why a run never happened; it simply falls outside
        // every future generation window.
        var template = TestPlanning.CreateTemplate();

        var result = template.AddException(new DateOnly(2020, 1, 1), ScheduleExceptionKind.Skip, null, null, null);

        Assert.True(result.IsSuccess);
    }

    // ----- Update -----

    [Fact]
    public void Update_edits_the_row_in_place_and_raises_the_event()
    {
        var template = TestPlanning.CreateTemplate();
        var id = template.AddException(TreatyDays, ScheduleExceptionKind.Skip, null, null, "Treaty Days").Value;
        template.ClearDomainEvents();

        var result = template.UpdateException(
            id, TreatyDays.AddDays(1), ScheduleExceptionKind.ExtraRun, new TimeOnly(9, 0), null, "  moved  ");

        Assert.True(result.IsSuccess);
        var exception = Assert.Single(template.Exceptions);
        Assert.Equal(TreatyDays.AddDays(1), exception.Date);
        Assert.Equal(ScheduleExceptionKind.ExtraRun, exception.Kind);
        Assert.Equal(new TimeOnly(9, 0), exception.DepartureTime);
        Assert.Equal("moved", exception.Note); // trimmed
        Assert.Contains(template.DomainEvents, e => e is ScheduleTemplateExceptionsChangedDomainEvent);
    }

    [Fact]
    public void Update_keeping_its_own_date_does_not_trip_the_duplicate_guard()
    {
        var template = TestPlanning.CreateTemplate();
        var id = template.AddException(TreatyDays, ScheduleExceptionKind.Skip, null, null, null).Value;

        var result = template.UpdateException(id, TreatyDays, ScheduleExceptionKind.Skip, null, null, "note added");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Update_onto_another_exceptions_date_fails_with_DuplicateExceptionDate()
    {
        var template = TestPlanning.CreateTemplate();
        template.AddException(TreatyDays, ScheduleExceptionKind.Skip, null, null, null);
        var otherId = template.AddException(TreatyDays.AddDays(1), ScheduleExceptionKind.Skip, null, null, null).Value;

        var result = template.UpdateException(otherId, TreatyDays, ScheduleExceptionKind.Skip, null, null, null);

        Assert.Equal(ScheduleTemplateErrors.DuplicateExceptionDate, result.Error);
    }

    [Fact]
    public void Update_of_a_missing_exception_fails_with_ExceptionNotFound()
    {
        var template = TestPlanning.CreateTemplate();

        var result = template.UpdateException(
            Guid.NewGuid(), TreatyDays, ScheduleExceptionKind.Skip, null, null, null);

        Assert.Equal(ScheduleTemplateErrors.ExceptionNotFound, result.Error);
    }

    // ----- Remove -----

    [Fact]
    public void Remove_deletes_the_row_and_raises_the_event()
    {
        var template = TestPlanning.CreateTemplate();
        var id = template.AddException(TreatyDays, ScheduleExceptionKind.Skip, null, null, null).Value;
        template.ClearDomainEvents();

        var result = template.RemoveException(id);

        Assert.True(result.IsSuccess);
        Assert.Empty(template.Exceptions);
        Assert.Contains(template.DomainEvents, e => e is ScheduleTemplateExceptionsChangedDomainEvent);
    }

    [Fact]
    public void Remove_of_a_missing_exception_fails_with_ExceptionNotFound()
    {
        var template = TestPlanning.CreateTemplate();

        var result = template.RemoveException(Guid.NewGuid());

        Assert.Equal(ScheduleTemplateErrors.ExceptionNotFound, result.Error);
    }

    [Fact]
    public void Blank_note_is_stored_as_null()
    {
        var template = TestPlanning.CreateTemplate();
        var id = template.AddException(TreatyDays, ScheduleExceptionKind.Skip, null, null, "   ").Value;

        var exception = Assert.Single(template.Exceptions, e => e.Id == id);
        Assert.Null(exception.Note);
    }
}
