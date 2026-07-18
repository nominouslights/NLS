using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Manifests.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class TripManifestCreateTests
{
    [Fact]
    public void Valid_app_manifest_is_created_and_raises_the_completed_event()
    {
        var result = TestManifests.Create();

        Assert.True(result.IsSuccess);
        var manifest = result.Value;
        Assert.Equal(TestManifests.TenantId, manifest.TenantId);
        Assert.Equal(ManifestSource.App, manifest.Source);
        Assert.Null(manifest.EnteredAt);
        Assert.Equal(28, manifest.PreTripItems.Count);
        Assert.Equal(6, manifest.PostTripItems.Count);

        var domainEvent = Assert.Single(manifest.DomainEvents);
        var completed = Assert.IsType<TripManifestCompletedDomainEvent>(domainEvent);
        Assert.Equal(manifest.Id, completed.ManifestId);
    }

    [Fact]
    public void Valid_paper_manifest_records_provenance()
    {
        var result = TestManifests.Create(source: ManifestSource.Paper, enteredBy: "Dispatch — R. Ballantyne");

        Assert.True(result.IsSuccess);
        Assert.Equal(ManifestSource.Paper, result.Value.Source);
        Assert.Equal("Dispatch — R. Ballantyne", result.Value.EnteredBy);
        Assert.NotNull(result.Value.EnteredAt);
    }

    [Fact]
    public void Paper_manifest_without_entered_by_is_rejected()
    {
        var result = TestManifests.Create(source: ManifestSource.Paper, enteredBy: "  ");

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.EnteredByRequired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void An_unchecked_attestation_is_rejected()
    {
        var result = TestManifests.Create(attestations: [true, true, false, true, true]);

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.AttestationsIncomplete, result.Error);
    }

    [Fact]
    public void A_missing_attestation_is_rejected()
    {
        var result = TestManifests.Create(attestations: [true, true, true, true]);

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.AttestationsIncomplete, result.Error);
    }

    [Fact]
    public void Missing_signature_is_rejected()
    {
        var result = TestManifests.Create(driverSignatureName: " ");

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.SignatureRequired, result.Error);
    }

    [Fact]
    public void End_odometer_below_start_is_rejected()
    {
        var result = TestManifests.Create(odometerStartKm: 118_346, odometerEndKm: 118_204);

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.OdometerEndBeforeStart, result.Error);
    }

    [Fact]
    public void Odometer_check_is_skipped_when_a_reading_is_missing()
    {
        var result = TestManifests.Create(odometerStartKm: null, odometerEndKm: 118_204);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void A_failed_pre_trip_item_without_a_note_is_rejected()
    {
        var preTrip = TestManifests.AllOkPreTrip();
        preTrip[3] = preTrip[3] with { Status = PreTripItemStatus.Fail, Severity = DefectSeverity.Minor, Note = null };

        var result = TestManifests.Create(preTrip: preTrip);

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.FailRequiresNote, result.Error);
    }

    [Fact]
    public void A_failed_pre_trip_item_with_a_note_is_accepted()
    {
        var preTrip = TestManifests.AllOkPreTrip();
        preTrip[3] = preTrip[3] with
        {
            Status = PreTripItemStatus.Fail,
            Severity = DefectSeverity.Minor,
            Note = "Streaking, replace blade",
        };

        var result = TestManifests.Create(preTrip: preTrip);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Fuel_added_without_litres_is_rejected()
    {
        var result = TestManifests.Create(fuelAdded: true, fuelLitres: null);

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.FuelLitresRequired, result.Error);
    }

    [Fact]
    public void More_than_eight_passengers_are_rejected()
    {
        var passengers = Enumerable.Range(1, 9)
            .Select(i => new ManifestPassenger { Name = $"Passenger {i}" })
            .ToList();

        var result = TestManifests.Create(passengers: passengers);

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.TooManyPassengers, result.Error);
    }

    [Fact]
    public void More_than_four_cargo_items_are_rejected()
    {
        var cargo = Enumerable.Range(1, 5)
            .Select(i => new ManifestCargoItem { Description = $"Crate {i}", Secured = true })
            .ToList();

        var result = TestManifests.Create(cargo: cargo);

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.TooManyCargoItems, result.Error);
    }

    [Theory]
    [InlineData("", "U-04", "J. Spence")]
    [InlineData("TR-4818", "", "J. Spence")]
    [InlineData("TR-4818", "U-04", "")]
    public void Missing_trip_identity_fields_are_rejected(string tripNumber, string unit, string driverName)
    {
        var result = TestManifests.Create(tripNumber: tripNumber, unit: unit, driverName: driverName);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Checklist_constants_match_the_printed_form()
    {
        Assert.Equal(28, ManifestChecklist.PreTripGroups.Sum(group => group.Value.Count));
        Assert.Equal(4, ManifestChecklist.PreTripGroups.Count);
        Assert.Equal(6, ManifestChecklist.PostTripItems.Count);
        Assert.Equal(5, ManifestChecklist.Attestations.Count);
    }
}
