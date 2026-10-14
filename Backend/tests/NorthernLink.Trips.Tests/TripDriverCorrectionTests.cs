using System.Reflection;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Domain.Trips.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// <see cref="Trip.CorrectDriver"/>: fixing the driver recorded on a finished run. Allowed only
/// in ReadyForBilling/Invoiced/Completed, reason required (≤ 500), and it touches the driver
/// snapshot and nothing else.
/// </summary>
public class TripDriverCorrectionTests
{
    private static readonly Guid NewDriverId = Guid.Parse("00000000-0000-0000-0000-0000000000d9");
    private const string NewDriverName = "J. Moose";
    private static readonly Guid ActorId = Guid.Parse("00000000-0000-0000-0000-00000000aa77");
    private const string ActorEmail = "dispatch@northernlink.ca";

    internal static Trip TripIn(TripStatus status)
    {
        var clientless = status == TripStatus.Completed;
        var trip = TestPlanning.ScheduleTrip(clientId: clientless ? null : Guid.NewGuid()).Value;

        switch (status)
        {
            case TripStatus.Scheduled:
                break;
            case TripStatus.InProgress:
                Assert.True(trip.Start().IsSuccess);
                break;
            case TripStatus.Cancelled:
                Assert.True(trip.Cancel("weather").IsSuccess);
                break;
            default:
                trip.RecordPostTripInspection();
                Assert.True(trip.FinishOperations().IsSuccess);
                if (status == TripStatus.Invoiced)
                {
                    Assert.True(trip.MarkInvoiced().IsSuccess);
                }
                else if (status == TripStatus.WrittenOff)
                {
                    Assert.True(trip.CloseWithoutBilling("goodwill run").IsSuccess);
                }

                break;
        }

        Assert.Equal(status, trip.Status);
        trip.ClearDomainEvents();
        return trip;
    }

    private static Result Correct(Trip trip, string reason = "Logged against the wrong driver", Guid? driverId = null) =>
        trip.CorrectDriver(driverId ?? NewDriverId, NewDriverName, reason, ActorId, ActorEmail);

    [Theory]
    [InlineData(TripStatus.ReadyForBilling)]
    [InlineData(TripStatus.Invoiced)]
    [InlineData(TripStatus.Completed)]
    public void Allowed_statuses_change_the_driver_and_nothing_else(TripStatus status)
    {
        var trip = TripIn(status);
        var before = Snapshot(trip);
        var updatedBefore = trip.UpdatedAtUtc;

        var result = Correct(trip);

        Assert.True(result.IsSuccess);
        Assert.Equal(NewDriverId, trip.DriverId);
        Assert.Equal(NewDriverName, trip.DriverName);
        Assert.True(trip.UpdatedAtUtc >= updatedBefore);

        var after = Snapshot(trip);
        foreach (var (name, value) in before)
        {
            if (name is nameof(Trip.DriverId) or nameof(Trip.DriverName) or nameof(Trip.UpdatedAtUtc))
            {
                continue;
            }

            Assert.True(Equals(value, after[name]), $"{name} changed: {value} → {after[name]}");
        }

        Assert.Equal(status, trip.Status);
    }

    [Theory]
    [InlineData(TripStatus.ReadyForBilling, true)]
    [InlineData(TripStatus.Invoiced, true)]
    [InlineData(TripStatus.Completed, true)]
    [InlineData(TripStatus.Scheduled, false)]
    [InlineData(TripStatus.InProgress, false)]
    [InlineData(TripStatus.Cancelled, false)]
    [InlineData(TripStatus.WrittenOff, false)]
    public void Allowed_for_matches_the_owner_decision(TripStatus status, bool allowed) =>
        Assert.Equal(allowed, Trip.DriverCorrectionAllowedFor(status));

    [Theory]
    [InlineData(TripStatus.Scheduled)]
    [InlineData(TripStatus.InProgress)]
    public void Operational_trips_are_sent_to_reassign(TripStatus status)
    {
        var trip = TripIn(status);

        var result = Correct(trip);

        Assert.True(result.IsFailure);
        Assert.Equal("Trips.Trip.UseReassign", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Contains(status.ToString(), result.Error.Message);
        Assert.Equal(TestPlanning.DriverId, trip.DriverId);
        Assert.Empty(trip.DomainEvents);
    }

    [Theory]
    [InlineData(TripStatus.Cancelled)]
    [InlineData(TripStatus.WrittenOff)]
    public void Cancelled_and_written_off_trips_are_refused_naming_the_status(TripStatus status)
    {
        var trip = TripIn(status);

        var result = Correct(trip);

        Assert.True(result.IsFailure);
        Assert.Equal("Trips.Trip.DriverCorrectionNotAllowed", result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Contains(status.ToString(), result.Error.Message);
        Assert.Equal(TestPlanning.DriverId, trip.DriverId);
        Assert.Empty(trip.DomainEvents);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_reason_is_refused(string? reason)
    {
        var trip = TripIn(TripStatus.ReadyForBilling);

        var result = trip.CorrectDriver(NewDriverId, NewDriverName, reason!, ActorId, ActorEmail);

        Assert.Equal(TripErrors.DriverCorrectionReasonRequired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(TestPlanning.DriverId, trip.DriverId);
    }

    [Fact]
    public void A_reason_over_500_characters_is_refused_but_500_after_trimming_is_fine()
    {
        var tooLong = TripIn(TripStatus.ReadyForBilling);
        var result = Correct(tooLong, new string('x', Trip.MaxReasonLength + 1));
        Assert.Equal(TripErrors.DriverCorrectionReasonTooLong, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);

        var exact = TripIn(TripStatus.ReadyForBilling);
        Assert.True(Correct(exact, "  " + new string('x', Trip.MaxReasonLength) + "  ").IsSuccess);
        Assert.Equal(500, Trip.MaxReasonLength);
    }

    [Fact]
    public void The_same_driver_is_refused()
    {
        var trip = TripIn(TripStatus.Invoiced);

        var result = Correct(trip, driverId: TestPlanning.DriverId);

        Assert.Equal(TripErrors.DriverUnchanged, result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Empty(trip.DomainEvents);
    }

    [Fact]
    public void A_trip_with_no_driver_on_record_can_be_given_one()
    {
        var trip = TestPlanning.ScheduleTrip().Value;
        Assert.True(trip.UnassignDriver().IsSuccess);
        trip.RecordPostTripInspection();
        Assert.True(trip.FinishOperations().IsSuccess); // clientless → Completed
        trip.ClearDomainEvents();

        Assert.True(Correct(trip).IsSuccess);

        var raised = Assert.IsType<TripDriverCorrectedDomainEvent>(Assert.Single(trip.DomainEvents));
        Assert.Null(raised.PreviousDriverId);
        Assert.Null(raised.PreviousDriverName);
        Assert.Equal(NewDriverId, trip.DriverId);
    }

    [Fact]
    public void Raises_only_the_correction_event_with_every_field()
    {
        var trip = TripIn(TripStatus.ReadyForBilling);

        Assert.True(Correct(trip, "  Logged against the wrong driver  ").IsSuccess);

        var raised = Assert.IsType<TripDriverCorrectedDomainEvent>(Assert.Single(trip.DomainEvents));
        Assert.Equal(trip.Id, raised.TripId);
        Assert.Equal(TestPlanning.DriverId, raised.PreviousDriverId);
        Assert.Equal(TestPlanning.DriverName, raised.PreviousDriverName);
        Assert.Equal(NewDriverId, raised.NewDriverId);
        Assert.Equal(NewDriverName, raised.NewDriverName);
        Assert.Equal("Logged against the wrong driver", raised.Reason);
        Assert.Equal(ActorId, raised.EnteredByUserId);
        Assert.Equal(ActorEmail, raised.EnteredBy);
        Assert.Empty(trip.DomainEvents.OfType<TripAssignmentChangedDomainEvent>());
    }

    [Fact]
    public void The_journal_name_and_payload_fields_are_what_the_activity_timeline_reads()
    {
        Assert.Equal("trip-driver-corrected", AuditNames.ForEvent(typeof(TripDriverCorrectedDomainEvent)));

        var trip = TripIn(TripStatus.Completed);
        Correct(trip);
        var payload = AuditJson.Serialize(trip.DomainEvents.Single());

        Assert.Contains("\"reason\":\"Logged against the wrong driver\"", payload);
        Assert.Contains("\"enteredBy\":\"dispatch@northernlink.ca\"", payload);
        Assert.Contains("\"previousDriverName\":\"R. Ballantyne\"", payload);
        Assert.Contains("\"newDriverName\":\"J. Moose\"", payload);
    }

    [Fact]
    public void Assign_is_still_refused_on_a_closed_trip()
    {
        var trip = TripIn(TripStatus.ReadyForBilling);

        Assert.Equal("Trips.Trip.OperationallyClosed", trip.AssignDriver(NewDriverId, NewDriverName).Error.Code);
    }

    /// <summary>Every public instance property's value, so "nothing else changed" is checked exhaustively.</summary>
    private static Dictionary<string, object?> Snapshot(Trip trip) =>
        typeof(Trip)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != nameof(Trip.DomainEvents))
            .ToDictionary(
                p => p.Name,
                p => p.GetValue(trip) is System.Collections.IEnumerable list and not string
                    ? string.Join("|", list.Cast<object?>().Select(o => o?.ToString()))
                    : p.GetValue(trip));
}
