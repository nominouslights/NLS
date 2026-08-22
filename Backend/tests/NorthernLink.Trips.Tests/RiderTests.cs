using NorthernLink.Trips.Domain.Riders;
using NorthernLink.Trips.Domain.Riders.Events;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class RiderTests
{
    // ---- Create ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_name_is_rejected(string name)
    {
        var result = TestRiders.Create(name: name);

        Assert.Equal(RiderErrors.NameRequired, result.Error);
    }

    [Fact]
    public void Creation_normalizes_the_dedup_key_and_raises_the_created_event()
    {
        var rider = TestRiders.Create(name: "  mary   Beardy ").Value;

        Assert.Equal("mary Beardy", rider.Name); // display keeps the given casing, collapsed
        Assert.Equal("MARY BEARDY", rider.NormalizedName);
        Assert.Equal(1, rider.TripCount);
        Assert.Equal(TestRiders.FirstTripDate, rider.LastTripDate);
        Assert.Equal(TestRiders.FirstTripNumber, rider.LastTripNumber);
        Assert.Null(rider.RotationDays);
        Assert.Contains(rider.DomainEvents, e => e is RiderCreatedDomainEvent);
    }

    [Fact]
    public void Normalize_name_trims_collapses_and_uppercases()
    {
        Assert.Equal("MARY BEARDY", Rider.NormalizeName("  mary \t beardy "));
    }

    // ---- SetRotation ----

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void Allowed_rotations_are_accepted_for_contract_crew(int days)
    {
        var rider = TestRiders.Create().Value;

        var result = rider.SetRotation(days);

        Assert.True(result.IsSuccess);
        Assert.Equal(days, rider.RotationDays);
        Assert.Contains(rider.DomainEvents, e => e is RiderRotationChangedDomainEvent);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Off_menu_rotations_are_rejected(int days)
    {
        var rider = TestRiders.Create().Value;

        var result = rider.SetRotation(days);

        Assert.Equal(RiderErrors.InvalidRotation, result.Error);
        Assert.Null(rider.RotationDays);
    }

    [Theory]
    [InlineData(TripServiceType.Community)]
    [InlineData(TripServiceType.Nihb)]
    [InlineData(TripServiceType.Charter)]
    public void Rotation_is_rejected_for_non_contract_crew(TripServiceType serviceType)
    {
        var rider = TestRiders.Create(serviceType: serviceType).Value;

        var result = rider.SetRotation(20);

        Assert.Equal(RiderErrors.RotationNotApplicable, result.Error);
        Assert.Null(rider.RotationDays);
    }

    [Fact]
    public void Rotation_is_clearable_with_null()
    {
        var rider = TestRiders.Create().Value;
        Assert.True(rider.SetRotation(20).IsSuccess);

        var result = rider.SetRotation(null);

        Assert.True(result.IsSuccess);
        Assert.Null(rider.RotationDays);
    }

    [Fact]
    public void Setting_the_same_rotation_again_is_a_no_op()
    {
        var rider = TestRiders.Create().Value;
        Assert.True(rider.SetRotation(20).IsSuccess);
        rider.ClearDomainEvents();

        var result = rider.SetRotation(20);

        Assert.True(result.IsSuccess);
        Assert.Empty(rider.DomainEvents);
    }

    // ---- RecordTrip ----

    [Fact]
    public void A_different_trip_number_advances_the_count_and_the_latest_trip_fields()
    {
        var rider = TestRiders.Create().Value;
        rider.ClearDomainEvents();

        rider.RecordTrip("M. Beardy", "204-555-0101", new DateOnly(2026, 7, 21), "TR-4901");

        Assert.Equal(2, rider.TripCount);
        Assert.Equal(new DateOnly(2026, 7, 21), rider.LastTripDate);
        Assert.Equal("TR-4901", rider.LastTripNumber);
        Assert.Equal("204-555-0101", rider.Contact);
        Assert.Contains(rider.DomainEvents, e => e is RiderTripRecordedDomainEvent);
    }

    [Fact]
    public void Redelivery_of_the_same_trip_number_converges_without_an_event()
    {
        var rider = TestRiders.Create().Value;
        rider.ClearDomainEvents();

        rider.RecordTrip("M. Beardy", "m.beardy@example.ca", TestRiders.FirstTripDate, TestRiders.FirstTripNumber);

        Assert.Equal(1, rider.TripCount);
        Assert.Empty(rider.DomainEvents);
    }

    [Fact]
    public void An_older_trip_never_regresses_the_latest_trip_fields()
    {
        var rider = TestRiders.Create().Value;
        rider.ClearDomainEvents();

        rider.RecordTrip("Mary Beardy", "204-555-0101", TestRiders.FirstTripDate.AddDays(-30), "TR-4700");

        // A genuinely different (earlier) trip still counts…
        Assert.Equal(2, rider.TripCount);
        Assert.Contains(rider.DomainEvents, e => e is RiderTripRecordedDomainEvent);
        // …but the latest-trip fields stay on the newer trip.
        Assert.Equal(TestRiders.FirstTripDate, rider.LastTripDate);
        Assert.Equal(TestRiders.FirstTripNumber, rider.LastTripNumber);
        Assert.Equal("M. Beardy", rider.Name);
        Assert.Equal("m.beardy@example.ca", rider.Contact);
    }

    [Fact]
    public void A_newer_trip_adopts_the_latest_spelling_and_contact()
    {
        var rider = TestRiders.Create().Value;

        rider.RecordTrip("Mary  Beardy", "204-555-0101", TestRiders.FirstTripDate.AddDays(7), "TR-4901");

        Assert.Equal("Mary Beardy", rider.Name);
        Assert.Equal("M. BEARDY", rider.NormalizedName); // the dedup key never moves
        Assert.Equal("204-555-0101", rider.Contact);
    }

    [Fact]
    public void A_manifest_without_contact_info_never_erases_a_known_contact()
    {
        var rider = TestRiders.Create().Value;

        rider.RecordTrip("M. Beardy", null, TestRiders.FirstTripDate.AddDays(7), "TR-4901");

        Assert.Equal("m.beardy@example.ca", rider.Contact);
    }
}
