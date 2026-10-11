using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Drivers.Domain.Hos.Events;
using NorthernLink.Shared.Kernel;
using Xunit;
using static NorthernLink.Drivers.Tests.Hos.HosTestSupport;
using HosException = NorthernLink.Drivers.Domain.Hos.HosException;

namespace NorthernLink.Drivers.Tests.Hos;

/// <summary>
/// The HosException aggregate: the regulation's shape of each exception (s.76 knowability and
/// the two-hour cap, s.16's trip and amount), the 24-hour declaration window, and the
/// lifecycle — who may withdraw at which status, and that a rejected or withdrawn exception
/// stops applying.
/// </summary>
public class HosExceptionTests
{
    private static readonly Guid OfficerId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid TripId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static Result<HosException> Declare(
        HosExceptionKind kind = HosExceptionKind.AdverseDrivingConditions,
        string? effectiveFrom = "10:00",
        DateTimeOffset? effectiveFromInstant = null,
        DateTimeOffset? effectiveTo = null,
        string? reason = "Whiteout on PR 391 north of Thompson",
        bool notKnown = true,
        TimeSpan? extra = null,
        bool adverseDefaultExtension = true) =>
        HosException.Declare(
            TestDrivers.TenantId, DriverId, kind, UserId, Now,
            effectiveFromInstant ?? At(effectiveFrom!), effectiveTo, reason, "PR 391 km 40", notKnown,
            extra ?? (adverseDefaultExtension && kind == HosExceptionKind.AdverseDrivingConditions ? TimeSpan.FromMinutes(90) : null),
            eldRemarkRecorded: true, tripId: TripId);

    private static Result<HosException> Deferral(
        Guid? tripId = null,
        DateTimeOffset? effectiveFrom = null,
        TimeSpan? deferred = null,
        string? reason = "Return leg from Lynn Lake runs past 22:00") =>
        HosException.ApproveDeferral(
            TestDrivers.TenantId, DriverId, OfficerId, Now,
            tripId ?? TripId, effectiveFrom ?? At("06:00", 1), deferred ?? TimeSpan.FromHours(1.5), reason, null, false);

    // ---- Declare: adverse driving conditions / emergency ----

    [Fact]
    public void Adverse_conditions_are_declared_applying_with_the_extension_and_one_event()
    {
        var result = Declare();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        var e = result.Value;
        Assert.Equal(HosExceptionStatus.Declared, e.Status);
        Assert.True(e.IsApplying);
        Assert.Equal(TimeSpan.FromMinutes(90), e.ExtraDriving);
        Assert.Null(e.DeferredOffDuty);
        Assert.Equal(UserId, e.DeclaredByUserId);
        Assert.Equal(Now, e.DeclaredAtUtc);
        Assert.Equal(At("10:00"), e.EffectiveFromUtc);
        Assert.Equal(TripId, e.TripId);
        Assert.True(e.EldRemarkRecorded);
        var raised = Assert.IsType<HosExceptionDeclaredDomainEvent>(Assert.Single(e.DomainEvents));
        Assert.Equal(HosExceptionKind.AdverseDrivingConditions, raised.Kind);
    }

    [Fact]
    public void Adverse_conditions_must_not_have_been_knowable_before_departure() =>
        Assert.Equal(ComplianceErrors.AdverseKnowabilityRequired, Declare(notKnown: false).Error);

    [Fact]
    public void Adverse_conditions_need_an_extension_of_at_least_a_minute()
    {
        Assert.Equal(ComplianceErrors.AdverseExtensionRequired, Declare(adverseDefaultExtension: false).Error);
        Assert.Equal(ComplianceErrors.AdverseExtensionRequired, Declare(extra: TimeSpan.FromSeconds(30)).Error);
    }

    [Fact]
    public void Adverse_conditions_extend_by_at_most_two_hours_inclusive()
    {
        Assert.True(Declare(extra: TimeSpan.FromHours(2)).IsSuccess);
        Assert.Equal(ComplianceErrors.AdverseExtensionTooLong, Declare(extra: TimeSpan.FromMinutes(121)).Error);
    }

    [Fact]
    public void An_emergency_needs_no_extension_and_carries_none()
    {
        var result = Declare(kind: HosExceptionKind.Emergency, notKnown: false, extra: TimeSpan.FromHours(5));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ExtraDriving);
        Assert.False(result.Value.NotKnownBeforeDeparture);
    }

    [Fact]
    public void A_declaration_must_be_within_24_hours_of_the_conditions_arising()
    {
        Assert.Equal(ComplianceErrors.ExceptionBackdated, Declare(effectiveFromInstant: Now.AddHours(-24).AddSeconds(-1)).Error);
        Assert.True(Declare(effectiveFromInstant: Now.AddHours(-24)).IsSuccess);
    }

    [Fact]
    public void A_declaration_cannot_start_more_than_five_minutes_ahead()
    {
        Assert.Equal(ComplianceErrors.ExceptionInFuture, Declare(effectiveFromInstant: Now.AddMinutes(6)).Error);
        Assert.True(Declare(effectiveFromInstant: Now.AddMinutes(5)).IsSuccess);
    }

    [Fact]
    public void The_span_cannot_end_before_it_starts() =>
        Assert.Equal(ComplianceErrors.ExceptionSpanInvalid, Declare(effectiveTo: At("09:59")).Error);

    [Fact]
    public void A_reason_is_required_and_capped()
    {
        Assert.Equal(ComplianceErrors.ExceptionReasonRequired, Declare(reason: "  ").Error);
        Assert.Equal(ComplianceErrors.ExceptionReasonTooLong, Declare(reason: new string('x', HosException.ReasonMaxLength + 1)).Error);
        Assert.True(Declare(reason: new string('x', HosException.ReasonMaxLength)).IsSuccess);
    }

    [Fact]
    public void A_deferral_cannot_be_declared_only_approved() =>
        Assert.Equal(ComplianceErrors.DeferralRequiresComplianceOfficer, Declare(kind: HosExceptionKind.OffDutyDeferral, notKnown: false).Error);

    // ---- ApproveDeferral ----

    [Fact]
    public void A_deferral_is_born_approved_for_its_trip_and_runs_to_the_end_of_day_two()
    {
        var result = Deferral();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        var e = result.Value;
        Assert.Equal(HosExceptionKind.OffDutyDeferral, e.Kind);
        Assert.Equal(HosExceptionStatus.Approved, e.Status);
        Assert.True(e.IsApplying);
        Assert.Equal(OfficerId, e.ApprovedByUserId);
        Assert.Equal(OfficerId, e.DeclaredByUserId);
        Assert.Equal(Now, e.ApprovedAtUtc);
        Assert.Equal(TripId, e.TripId);
        Assert.Equal(TimeSpan.FromMinutes(90), e.DeferredOffDuty);
        Assert.Null(e.ExtraDriving);

        // Day one is D+1 (America/Winnipeg); the exception runs to the end of D+2.
        Assert.Equal(At("00:00", 3), e.EffectiveToUtc);
        Assert.IsType<HosExceptionDeclaredDomainEvent>(Assert.Single(e.DomainEvents));
    }

    [Fact]
    public void A_deferral_names_its_trip() =>
        Assert.Equal(ComplianceErrors.DeferralTripRequired, Deferral(tripId: Guid.Empty).Error);

    [Fact]
    public void A_deferral_starts_in_the_future_and_within_seven_days()
    {
        Assert.Equal(ComplianceErrors.DeferralNotInFuture, Deferral(effectiveFrom: Now).Error);
        Assert.Equal(ComplianceErrors.DeferralTooFarAhead, Deferral(effectiveFrom: Now.AddDays(7)).Error);
        Assert.True(Deferral(effectiveFrom: Now.AddDays(7).AddSeconds(-1)).IsSuccess);
    }

    [Fact]
    public void A_deferral_defers_between_a_minute_and_two_hours()
    {
        Assert.Equal(ComplianceErrors.DeferralAmountInvalid, Deferral(deferred: TimeSpan.Zero).Error);
        Assert.Equal(ComplianceErrors.DeferralAmountInvalid, Deferral(deferred: TimeSpan.FromMinutes(121)).Error);
        Assert.True(Deferral(deferred: TimeSpan.FromHours(2)).IsSuccess);
    }

    // ---- Lifecycle ----

    [Fact]
    public void Acknowledging_moves_declared_to_acknowledged_once()
    {
        var e = Declare().Value;
        e.ClearDomainEvents();

        Assert.True(e.Acknowledge(OfficerId, Now.AddMinutes(5)).IsSuccess);
        Assert.Equal(HosExceptionStatus.Acknowledged, e.Status);
        Assert.Equal(OfficerId, e.DispatchAcknowledgedByUserId);
        Assert.True(e.IsApplying);
        Assert.IsType<HosExceptionStatusChangedDomainEvent>(Assert.Single(e.DomainEvents));

        e.ClearDomainEvents();
        Assert.True(e.Acknowledge(OfficerId, Now.AddMinutes(10)).IsSuccess);
        Assert.Empty(e.DomainEvents);
        Assert.Equal(Now.AddMinutes(5), e.DispatchAcknowledgedAtUtc);
    }

    [Fact]
    public void Rejecting_stops_the_exception_applying_and_records_who_and_why()
    {
        var e = Declare().Value;
        e.ClearDomainEvents();

        Assert.True(e.Reject(OfficerId, Now.AddHours(1), "Forecast was published the night before").IsSuccess);

        Assert.Equal(HosExceptionStatus.Rejected, e.Status);
        Assert.False(e.IsApplying);
        Assert.Equal(OfficerId, e.ClosedByUserId);
        Assert.Equal("Forecast was published the night before", e.ClosureNote);
        Assert.IsType<HosExceptionStatusChangedDomainEvent>(Assert.Single(e.DomainEvents));

        e.ClearDomainEvents();
        Assert.True(e.Reject(OfficerId, Now.AddHours(2), null).IsSuccess);
        Assert.Empty(e.DomainEvents);
        Assert.Equal(ComplianceErrors.ExceptionClosed, e.Withdraw(UserId, Now.AddHours(2), complianceOfficer: true, null).Error);
        Assert.Equal(ComplianceErrors.ExceptionClosed, e.Acknowledge(OfficerId, Now.AddHours(2)).Error);
    }

    [Fact]
    public void The_declarer_may_withdraw_only_while_still_declared()
    {
        var e = Declare().Value;
        Assert.True(e.Withdraw(UserId, Now.AddMinutes(1), complianceOfficer: false, null).IsSuccess);
        Assert.Equal(HosExceptionStatus.Withdrawn, e.Status);
        Assert.False(e.IsApplying);

        var acknowledged = Declare().Value;
        Assert.True(acknowledged.Acknowledge(OfficerId, Now).IsSuccess);
        Assert.Equal(
            ComplianceErrors.ExceptionWithdrawRequiresComplianceOfficer,
            acknowledged.Withdraw(UserId, Now, complianceOfficer: false, null).Error);
        Assert.True(acknowledged.Withdraw(OfficerId, Now, complianceOfficer: true, "driver asked").IsSuccess);
        Assert.Equal(HosExceptionStatus.Withdrawn, acknowledged.Status);
    }

    [Fact]
    public void A_withdrawn_exception_cannot_be_rejected()
    {
        var e = Declare().Value;
        Assert.True(e.Withdraw(UserId, Now, complianceOfficer: false, null).IsSuccess);

        Assert.Equal(ComplianceErrors.ExceptionClosed, e.Reject(OfficerId, Now, null).Error);
        Assert.True(e.Withdraw(UserId, Now, complianceOfficer: false, null).IsSuccess);
    }

    [Fact]
    public void Overlap_is_half_open_on_the_day_and_open_ended_for_an_unfinished_exception()
    {
        var open = Declare().Value;
        Assert.True(open.Overlaps(At("00:00").ToUnixTimeSeconds(), At("00:00", 1).ToUnixTimeSeconds()));
        Assert.True(open.Overlaps(At("00:00", 5).ToUnixTimeSeconds(), At("00:00", 6).ToUnixTimeSeconds()));
        Assert.False(open.Overlaps(At("00:00", -1).ToUnixTimeSeconds(), At("00:00").ToUnixTimeSeconds()));

        var closed = Declare(effectiveTo: At("11:00")).Value;
        Assert.False(closed.Overlaps(At("00:00", 1).ToUnixTimeSeconds(), At("00:00", 2).ToUnixTimeSeconds()));
    }
}
