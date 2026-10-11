using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Drivers.Domain.Hos.Events;
using NorthernLink.Shared.Kernel;
using Xunit;
using static NorthernLink.Drivers.Tests.Hos.HosTestSupport;

namespace NorthernLink.Drivers.Tests.Hos;

/// <summary>
/// The DutyDay aggregate: the well-formedness rules of a day's timeline, the idempotent append
/// the offline Field App relies on, personal conveyance's odometer and attestation rules and
/// the 75 km switch, amendment after certification, and certification's own guards.
/// </summary>
public class DutyDayTests
{
    // ---- Append ----

    [Fact]
    public void A_first_event_is_recorded_and_raises_one_event()
    {
        var day = NewDay();
        var draft = Draft("06:00", DutyStatus.OnDuty);

        var result = day.Append(draft, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(DutyAppendOutcome.Recorded, result.Value);
        var e = Assert.Single(day.Events);
        Assert.Equal(draft.EventId, e.EventId);
        Assert.Equal(1, e.Sequence);
        Assert.Equal(Now, e.RecordedAtUtc);
        Assert.Equal(UserId, e.RecordedByUserId);
        Assert.False(e.IsVoided);
        var raised = Assert.IsType<DutyEventAppendedDomainEvent>(Assert.Single(day.DomainEvents));
        Assert.Equal(draft.EventId, raised.EventId);
        Assert.Equal(D, raised.LocalDate);
    }

    /// <summary>
    /// The contract the Driver Field App's sync queue is built on: a replayed item is recognised
    /// and nothing happens — no second event in the timeline, and no domain event either, so
    /// the save that follows is a no-op rather than an eventless write the DbContext would
    /// refuse.
    /// </summary>
    [Fact]
    public void Appending_an_event_id_already_on_the_day_is_a_no_op_with_no_domain_event()
    {
        var id = Guid.NewGuid();
        var day = DayWith(Draft("06:00", DutyStatus.OnDuty, eventId: id));

        var replay = day.Append(Draft("09:00", DutyStatus.Driving, eventId: id), Now);

        Assert.True(replay.IsSuccess);
        Assert.Equal(DutyAppendOutcome.AlreadyRecorded, replay.Value);
        Assert.Single(day.Events);
        Assert.Empty(day.DomainEvents);
        Assert.Equal(DutyStatus.OnDuty, day.Events[0].Status);
    }

    [Fact]
    public void A_voided_event_id_still_counts_as_already_recorded()
    {
        var id = Guid.NewGuid();
        var day = DayWith(Draft("06:00", DutyStatus.OnDuty, eventId: id));
        Assert.True(day.Amend(id, Draft("06:05", DutyStatus.OnDuty), "typo", Now).IsSuccess);
        day.ClearDomainEvents();

        var replay = day.Append(Draft("06:00", DutyStatus.OnDuty, eventId: id), Now);

        Assert.Equal(DutyAppendOutcome.AlreadyRecorded, replay.Value);
        Assert.Empty(day.DomainEvents);
    }

    [Theory]
    [InlineData("23:59", -1)]
    [InlineData("00:00", 1)]
    public void An_event_outside_the_local_day_is_refused(string hhmm, int dayOffset)
    {
        var day = NewDay();

        var result = day.Append(Draft(hhmm, DutyStatus.OnDuty, dayOffset: dayOffset), At("12:00", 1));

        Assert.True(result.IsFailure);
        Assert.Equal(HosErrors.EventOutsideDay, result.Error);
        Assert.Empty(day.Events);
    }

    [Fact]
    public void The_first_and_last_second_of_the_local_day_are_inside_it()
    {
        var day = NewDay();

        Assert.True(day.Append(Draft("00:00", DutyStatus.OffDuty), At("12:00", 1)).IsSuccess);
        Assert.True(day.Append(Draft("23:59", DutyStatus.OnDuty), At("12:00", 1)).IsSuccess);
    }

    [Fact]
    public void An_event_more_than_five_minutes_in_the_future_is_refused()
    {
        var day = NewDay();

        var result = day.Append(Draft("15:06", DutyStatus.OnDuty), Now);

        Assert.Equal(HosErrors.EventInFuture, result.Error);
    }

    [Fact]
    public void Five_minutes_of_clock_skew_is_tolerated()
    {
        var day = NewDay();

        Assert.True(day.Append(Draft("15:05", DutyStatus.OnDuty), Now).IsSuccess);
    }

    [Fact]
    public void Repeating_the_status_in_force_is_refused()
    {
        var day = DayWith(Draft("06:00", DutyStatus.OnDuty), Draft("06:30", DutyStatus.Driving));

        var result = day.Append(Draft("07:00", DutyStatus.Driving), Now);

        Assert.Equal(HosErrors.NoStatusChange, result.Error);
        Assert.Equal(2, day.Events.Count);
    }

    [Fact]
    public void An_event_inserted_earlier_is_checked_against_the_status_in_force_at_its_instant()
    {
        var day = DayWith(Draft("06:00", DutyStatus.OnDuty), Draft("08:00", DutyStatus.Driving));

        // At 07:00 the driver was On Duty, so On Duty again is a repeat; Off Duty is a change.
        Assert.Equal(HosErrors.NoStatusChange, day.Append(Draft("07:00", DutyStatus.OnDuty), Now).Error);
        Assert.True(day.Append(Draft("07:00", DutyStatus.OffDuty), Now).IsSuccess);
        Assert.Equal([DutyStatus.OnDuty, DutyStatus.OffDuty, DutyStatus.Driving], day.EffectiveTimeline().Select(e => e.Status));
    }

    [Theory]
    [InlineData(DutyEventSource.EldTranscription)]
    [InlineData(DutyEventSource.PaperLog)]
    public void A_transcribed_event_needs_the_dispatchers_name(DutyEventSource source)
    {
        var day = NewDay();

        Assert.Equal(HosErrors.EnteredByRequired, day.Append(Draft("06:00", DutyStatus.OnDuty, source), Now).Error);
        Assert.True(day.Append(Draft("06:00", DutyStatus.OnDuty, source, enteredBy: "D. Wells"), Now).IsSuccess);
    }

    [Fact]
    public void Other_carrier_work_needs_the_carriers_name()
    {
        var day = NewDay();

        Assert.Equal(HosErrors.CarrierNameRequired, day.Append(Draft("06:00", DutyStatus.OnDuty, DutyEventSource.OtherCarrier), Now).Error);
        Assert.True(day.Append(Draft("06:00", DutyStatus.OnDuty, DutyEventSource.OtherCarrier, carrierName: "Keewatin Rail"), Now).IsSuccess);
    }

    [Fact]
    public void The_system_source_cannot_be_submitted()
    {
        var day = NewDay();

        Assert.Equal(HosErrors.SystemSourceReserved, day.Append(Draft("06:00", DutyStatus.OnDuty, DutyEventSource.System), Now).Error);
    }

    // ---- Personal conveyance ----

    [Fact]
    public void Personal_conveyance_needs_a_start_odometer()
    {
        var day = DayWith(Draft("06:00", DutyStatus.OffDuty));

        var result = day.Append(Draft("10:00", DutyStatus.PersonalConveyance, attest: true), Now);

        Assert.Equal(HosErrors.PcOdometerRequired, result.Error);
    }

    [Fact]
    public void Personal_conveyance_needs_all_three_attestations()
    {
        var day = DayWith(Draft("06:00", DutyStatus.OffDuty));
        var partial = Draft("10:00", DutyStatus.PersonalConveyance, startKm: 1000m) with { PcNoPassengersOrCargo = true, PcNoTrailer = true };

        var result = day.Append(partial, Now);

        Assert.Equal(HosErrors.PcAttestationRequired, result.Error);
    }

    [Fact]
    public void The_event_closing_personal_conveyance_needs_an_end_odometer_not_below_the_start()
    {
        var day = DayWith(
            Draft("06:00", DutyStatus.OffDuty),
            Draft("10:00", DutyStatus.PersonalConveyance, startKm: 1000m, attest: true));

        Assert.Equal(HosErrors.PcOdometerEndRequired, day.Append(Draft("11:00", DutyStatus.OffDuty), Now).Error);
        Assert.Equal(HosErrors.PcOdometerDecreasing, day.Append(Draft("11:00", DutyStatus.OffDuty, endKm: 999.9m), Now).Error);

        Assert.True(day.Append(Draft("11:00", DutyStatus.OffDuty, endKm: 1040.5m), Now).IsSuccess);
        Assert.Equal(40.5m, day.PersonalConveyanceKm);
    }

    [Fact]
    public void Exactly_seventy_five_kilometres_is_still_personal_conveyance()
    {
        var day = DayWith(
            Draft("06:00", DutyStatus.OffDuty),
            Draft("10:00", DutyStatus.PersonalConveyance, startKm: 1000m, attest: true));

        Assert.True(day.Append(Draft("12:00", DutyStatus.OffDuty, endKm: 1075m), Now).IsSuccess);

        Assert.Equal(75m, day.PersonalConveyanceKm);
        Assert.DoesNotContain(day.Events, e => e.Source == DutyEventSource.System);
    }

    /// <summary>
    /// 100 km over two hours with 75 km allowed: the allowance ran out 75% of the way through,
    /// at 11:30. The switch is a System on-duty event at that instant carrying the interpolated
    /// odometer, so the engine credits exactly 75.0 km of personal conveyance and counts the
    /// rest as work — the same thing a compliant ELD does.
    /// </summary>
    [Fact]
    public void Crossing_seventy_five_kilometres_appends_a_system_on_duty_event_at_the_interpolated_instant()
    {
        var day = DayWith(
            Draft("06:00", DutyStatus.OffDuty),
            Draft("10:00", DutyStatus.PersonalConveyance, startKm: 1000m, attest: true));

        var result = day.Append(Draft("12:00", DutyStatus.OffDuty, endKm: 1100m), Now);

        Assert.True(result.IsSuccess);
        var system = Assert.Single(day.Events, e => e.Source == DutyEventSource.System);
        Assert.Equal(DutyStatus.OnDuty, system.Status);
        Assert.Equal(At("11:30"), system.AtUtc);
        Assert.Equal(1075m, system.OdometerEndKm);
        Assert.Equal(DutyDay.PcLimitReachedNote, system.Note);
        Assert.Equal(75m, day.PersonalConveyanceKm);
        Assert.Equal(
            [DutyStatus.OffDuty, DutyStatus.PersonalConveyance, DutyStatus.OnDuty, DutyStatus.OffDuty],
            day.EffectiveTimeline().Select(e => e.Status));

        // The driver's own closing event and the system switch each raise an event.
        Assert.Equal(2, day.DomainEvents.Count);
        Assert.All(day.DomainEvents, e => Assert.IsType<DutyEventAppendedDomainEvent>(e));
    }

    [Fact]
    public void The_allowance_is_shared_across_segments_in_the_day()
    {
        var day = DayWith(
            Draft("06:00", DutyStatus.OffDuty),
            Draft("08:00", DutyStatus.PersonalConveyance, startKm: 1000m, attest: true),
            Draft("09:00", DutyStatus.OffDuty, endKm: 1050m),
            Draft("10:00", DutyStatus.PersonalConveyance, startKm: 1050m, attest: true));

        // 50 km already used; 25 km left of a 50 km segment over 100 minutes → switch at 10:50.
        Assert.True(day.Append(Draft("11:40", DutyStatus.OffDuty, endKm: 1100m), Now).IsSuccess);

        var system = Assert.Single(day.Events, e => e.Source == DutyEventSource.System);
        Assert.Equal(At("10:50"), system.AtUtc);
        Assert.Equal(1075m, system.OdometerEndKm);
        Assert.Equal(75m, day.PersonalConveyanceKm);
    }

    [Fact]
    public void Personal_conveyance_cannot_start_once_the_allowance_is_used_up()
    {
        var day = DayWith(
            Draft("06:00", DutyStatus.OffDuty),
            Draft("08:00", DutyStatus.PersonalConveyance, startKm: 1000m, attest: true),
            Draft("09:00", DutyStatus.OffDuty, endKm: 1075m));

        var result = day.Append(Draft("10:00", DutyStatus.PersonalConveyance, startKm: 1075m, attest: true), Now);

        Assert.Equal(HosErrors.PcAllowanceExhausted, result.Error);
    }

    // ---- Amend ----

    [Fact]
    public void Amending_voids_the_original_keeps_it_and_appends_the_replacement_with_the_reason()
    {
        var original = Guid.NewGuid();
        var day = DayWith(Draft("06:00", DutyStatus.OnDuty, eventId: original), Draft("08:00", DutyStatus.Driving));
        var replacement = Draft("06:15", DutyStatus.OnDuty);

        var result = day.Amend(original, replacement, "  ELD shows 06:15  ", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(DutyAppendOutcome.Recorded, result.Value);
        Assert.Equal(3, day.Events.Count);
        var voided = Assert.Single(day.Events, e => e.EventId == original);
        Assert.True(voided.IsVoided);
        var appended = Assert.Single(day.Events, e => e.EventId == replacement.EventId);
        Assert.Equal(original, appended.AmendsEventId);
        Assert.Equal("ELD shows 06:15", appended.AmendmentReason);
        Assert.Equal([replacement.EventId], day.EffectiveTimeline().Where(e => e.Status == DutyStatus.OnDuty).Select(e => e.EventId));
        var raised = Assert.IsType<DutyEventAmendedDomainEvent>(Assert.Single(day.DomainEvents));
        Assert.Equal(original, raised.OriginalEventId);
        Assert.Equal(replacement.EventId, raised.ReplacementEventId);
    }

    [Fact]
    public void An_amendment_needs_a_reason_and_an_existing_unvoided_original()
    {
        var original = Guid.NewGuid();
        var day = DayWith(Draft("06:00", DutyStatus.OnDuty, eventId: original));

        Assert.Equal(HosErrors.AmendmentReasonRequired, day.Amend(original, Draft("06:15", DutyStatus.OnDuty), " ", Now).Error);
        Assert.Equal(HosErrors.EventNotFound, day.Amend(Guid.NewGuid(), Draft("06:15", DutyStatus.OnDuty), "x", Now).Error);

        Assert.True(day.Amend(original, Draft("06:15", DutyStatus.OnDuty), "x", Now).IsSuccess);
        Assert.Equal(HosErrors.EventAlreadyVoided, day.Amend(original, Draft("06:20", DutyStatus.OnDuty), "y", Now).Error);
    }

    [Fact]
    public void An_amendment_is_validated_against_the_timeline_without_the_original()
    {
        var original = Guid.NewGuid();
        var day = DayWith(
            Draft("06:00", DutyStatus.OnDuty),
            Draft("08:00", DutyStatus.Driving, eventId: original),
            Draft("12:00", DutyStatus.OffDuty));

        // Replacing the 08:00 Driving with Off Duty at 08:00 is a real change from On Duty;
        // replacing it with On Duty would repeat the status in force once the original is gone.
        Assert.Equal(HosErrors.NoStatusChange, day.Amend(original, Draft("08:00", DutyStatus.OnDuty), "x", Now).Error);
        Assert.True(day.Amend(original, Draft("08:00", DutyStatus.OffDuty), "x", Now).IsSuccess);
    }

    [Fact]
    public void Amending_a_certified_day_requires_recertification()
    {
        var original = Guid.NewGuid();
        var day = DayWith(Draft("06:00", DutyStatus.OnDuty, eventId: original), Draft("14:00", DutyStatus.OffDuty));
        Assert.True(day.Certify(UserId, Now).IsSuccess);
        Assert.True(day.IsCertified);
        day.ClearDomainEvents();

        Assert.True(day.Amend(original, Draft("06:10", DutyStatus.OnDuty), "ELD shows 06:10", Now).IsSuccess);

        Assert.True(day.RecertificationRequired);
        Assert.False(day.IsCertified);
        Assert.NotNull(day.CertifiedAtUtc);

        // Re-certifying clears the flag and raises again.
        Assert.True(day.Certify(UserId, Now).IsSuccess);
        Assert.True(day.IsCertified);
        Assert.False(day.RecertificationRequired);
        Assert.Contains(day.DomainEvents, e => e is DutyDayCertifiedDomainEvent);
    }

    [Fact]
    public void Amending_a_replayed_replacement_id_is_a_no_op()
    {
        var original = Guid.NewGuid();
        var replacementId = Guid.NewGuid();
        var day = DayWith(Draft("06:00", DutyStatus.OnDuty, eventId: original));
        Assert.True(day.Amend(original, Draft("06:15", DutyStatus.OnDuty, eventId: replacementId), "x", Now).IsSuccess);
        day.ClearDomainEvents();

        var replay = day.Amend(original, Draft("06:15", DutyStatus.OnDuty, eventId: replacementId), "x", Now);

        Assert.Equal(DutyAppendOutcome.AlreadyRecorded, replay.Value);
        Assert.Empty(day.DomainEvents);
        Assert.Equal(2, day.Events.Count);
    }

    [Fact]
    public void Amending_the_event_that_closed_personal_conveyance_re_derives_the_system_switch()
    {
        var closer = Guid.NewGuid();
        var day = DayWith(
            Draft("06:00", DutyStatus.OffDuty),
            Draft("10:00", DutyStatus.PersonalConveyance, startKm: 1000m, attest: true),
            Draft("12:00", DutyStatus.OffDuty, endKm: 1100m, eventId: closer));
        var firstSwitch = Assert.Single(day.Events, e => e.Source == DutyEventSource.System);

        // The odometer was misread: 1050, not 1100. Inside the allowance now — no switch.
        Assert.True(day.Amend(closer, Draft("12:00", DutyStatus.OffDuty, endKm: 1050m), "odometer misread", Now).IsSuccess);

        Assert.True(day.Events.Single(e => e.EventId == firstSwitch.EventId).IsVoided);
        Assert.DoesNotContain(day.EffectiveTimeline(), e => e.Source == DutyEventSource.System);
        Assert.Equal(50m, day.PersonalConveyanceKm);
    }

    // ---- Certify ----

    [Fact]
    public void Certifying_records_the_driver_and_the_instant()
    {
        var day = DayWith(Draft("06:00", DutyStatus.OnDuty), Draft("14:00", DutyStatus.OffDuty));

        var result = day.Certify(UserId, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(Now, day.CertifiedAtUtc);
        Assert.Equal(UserId, day.CertifiedByUserId);
        Assert.True(day.IsCertified);
        Assert.IsType<DutyDayCertifiedDomainEvent>(Assert.Single(day.DomainEvents));
    }

    [Fact]
    public void Certifying_again_with_nothing_to_recertify_is_a_no_op()
    {
        var day = DayWith(Draft("06:00", DutyStatus.OnDuty));
        Assert.True(day.Certify(UserId, Now).IsSuccess);
        day.ClearDomainEvents();

        Assert.True(day.Certify(UserId, Now.AddHours(1)).IsSuccess);

        Assert.Empty(day.DomainEvents);
        Assert.Equal(Now, day.CertifiedAtUtc);
    }

    [Fact]
    public void A_day_ending_in_open_personal_conveyance_cannot_be_certified()
    {
        var day = DayWith(
            Draft("06:00", DutyStatus.OffDuty),
            Draft("10:00", DutyStatus.PersonalConveyance, startKm: 1000m, attest: true));

        var result = day.Certify(UserId, Now);

        Assert.Equal(HosErrors.PcSegmentOpen, result.Error);
        Assert.Null(day.CertifiedAtUtc);
    }

    [Fact]
    public void A_day_that_has_not_started_cannot_be_certified()
    {
        var tomorrow = NewDay(D.AddDays(1));

        var result = tomorrow.Certify(UserId, Now);

        Assert.Equal(HosErrors.DayInFuture, result.Error);
    }

    [Fact]
    public void An_empty_day_cannot_be_certified()
    {
        Assert.Equal(HosErrors.NothingToCertify, NewDay().Certify(UserId, Now).Error);
    }

    [Fact]
    public void Odometers_are_stored_to_one_decimal_and_never_negative()
    {
        var day = DayWith(Draft("06:00", DutyStatus.OffDuty));

        Assert.Equal(HosErrors.OdometerNegative, day.Append(Draft("10:00", DutyStatus.PersonalConveyance, startKm: -1m, attest: true), Now).Error);
        Assert.True(day.Append(Draft("10:00", DutyStatus.PersonalConveyance, startKm: 1000.04m, attest: true), Now).IsSuccess);
        Assert.Equal(1000.0m, day.Events[^1].OdometerStartKm);
    }

    [Fact]
    public void Error_types_map_to_the_right_http_class()
    {
        Assert.Equal(ErrorType.Validation, HosErrors.EventOutsideDay.Type);
        Assert.Equal(ErrorType.NotFound, HosErrors.EventNotFound.Type);
        Assert.Equal(ErrorType.Conflict, HosErrors.EventAlreadyVoided.Type);
        Assert.Equal(ErrorType.NotFound, HosErrors.DayNotFound.Type);
    }
}
