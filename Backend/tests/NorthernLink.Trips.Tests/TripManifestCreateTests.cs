using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Application.Manifests;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Manifests.Events;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class TripManifestCreateTests
{
    [Fact]
    public void Valid_app_manifest_is_created_and_raises_the_created_event()
    {
        var result = TestManifests.Create();

        Assert.True(result.IsSuccess);
        var manifest = result.Value;
        Assert.Equal(TestManifests.TenantId, manifest.TenantId);
        Assert.Equal(ManifestSource.App, manifest.Source);
        Assert.Null(manifest.EnteredAt);
        Assert.Single(manifest.Passengers);

        var domainEvent = Assert.Single(manifest.DomainEvents);
        var created = Assert.IsType<TripManifestCreatedDomainEvent>(domainEvent);
        Assert.Equal(manifest.Id, created.ManifestId);
    }

    [Fact]
    public void A_manifest_is_valid_with_just_a_passenger_and_no_cargo()
    {
        var result = TestManifests.Create(cargo: [], allCargoSecured: CargoSecuredStatus.NotApplicable);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Cargo);
    }

    [Fact]
    public void A_manifest_is_valid_with_zero_passengers()
    {
        // No attestations, signature, or inspection are required anymore — passengers >= 0.
        var result = TestManifests.Create(passengers: []);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Passengers);
    }

    [Fact]
    public void Route_stop_referenced_passengers_round_trip()
    {
        var pickupId = Guid.NewGuid();
        var dropoffId = Guid.NewGuid();
        var passenger = new ManifestPassenger
        {
            Name = "R. Spence",
            PickupStopId = pickupId,
            PickupStopName = "Leaf Rapids",
            DropoffStopId = dropoffId,
            DropoffStopName = "Lynn Lake",
            BoardedOn = true,
        };

        var manifest = TestManifests.Create(passengers: [passenger]).Value;

        var stored = Assert.Single(manifest.Passengers);
        Assert.Equal(pickupId, stored.PickupStopId);
        Assert.Equal("Leaf Rapids", stored.PickupStopName);
        Assert.Equal(dropoffId, stored.DropoffStopId);
        Assert.Equal("Lynn Lake", stored.DropoffStopName);
    }

    [Fact]
    public void Free_form_passengers_leave_the_stop_ids_null()
    {
        var passenger = new ManifestPassenger { Name = "Walk-up rider" };

        var manifest = TestManifests.Create(passengers: [passenger]).Value;

        var stored = Assert.Single(manifest.Passengers);
        Assert.Null(stored.PickupStopId);
        Assert.Null(stored.DropoffStopId);
    }

    [Theory]
    [InlineData("rider@example.ca", null)]           // email-only
    [InlineData(null, "204-555-0134")]               // phone-only
    [InlineData("rider@example.ca", "204-555-0134")] // both set
    public void Passenger_email_and_phone_round_trip_onto_the_aggregate(string? email, string? phone)
    {
        var passenger = new ManifestPassenger
        {
            Name = "R. Spence",
            Email = email,
            Phone = phone,
            BoardedOn = true,
        };

        var manifest = TestManifests.Create(passengers: [passenger]).Value;

        var stored = Assert.Single(manifest.Passengers);
        Assert.Equal(email, stored.Email);
        Assert.Equal(phone, stored.Phone);
    }

    [Theory]
    [InlineData("rider@example.ca", null)]           // email-only
    [InlineData(null, "204-555-0134")]               // phone-only
    [InlineData("rider@example.ca", "204-555-0134")] // both set
    public void Passenger_email_and_phone_survive_the_response_mapper(string? email, string? phone)
    {
        var passenger = new ManifestPassenger
        {
            Name = "R. Spence",
            Email = email,
            Phone = phone,
            BoardedOn = true,
        };
        var manifest = TestManifests.Create(passengers: [passenger]).Value;

        var response = TripManifestResponseMapper.ToResponse(manifest);

        var mapped = Assert.Single(response.Passengers);
        Assert.Equal(email, mapped.Email);
        Assert.Equal(phone, mapped.Phone);
    }

    [Fact]
    public void Update_carries_the_new_email_and_phone_fields_onto_the_aggregate()
    {
        var manifest = TestManifests.Create().Value;

        var revised = new ManifestPassenger
        {
            Name = "J. Linklater",
            Email = "j.linklater@example.ca",
            Phone = "204-555-0199",
            BoardedOn = true,
        };

        var result = manifest.Update(
            manifest.TripDate,
            manifest.TripNumber,
            manifest.Route,
            manifest.Direction,
            manifest.Client,
            [revised],
            manifest.AllSeatbeltsVerified,
            manifest.Cargo,
            manifest.AllCargoSecured,
            ManifestSource.Dispatcher,
            enteredBy: "Dispatch — R. Ballantyne");

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(manifest.Passengers);
        Assert.Equal("j.linklater@example.ca", stored.Email);
        Assert.Equal("204-555-0199", stored.Phone);
    }

    [Fact]
    public void Valid_dispatcher_manifest_records_provenance()
    {
        var result = TestManifests.Create(source: ManifestSource.Dispatcher, enteredBy: "Dispatch — R. Ballantyne");

        Assert.True(result.IsSuccess);
        Assert.Equal(ManifestSource.Dispatcher, result.Value.Source);
        Assert.Equal("Dispatch — R. Ballantyne", result.Value.EnteredBy);
        Assert.NotNull(result.Value.EnteredAt);
    }

    [Fact]
    public void Dispatcher_manifest_without_entered_by_is_rejected()
    {
        var result = TestManifests.Create(source: ManifestSource.Dispatcher, enteredBy: "  ");

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.EnteredByRequired, result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void A_missing_trip_number_is_rejected()
    {
        var result = TestManifests.Create(tripNumber: "");

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.TripNumberRequired, result.Error);
    }

    [Fact]
    public void A_passenger_without_a_name_is_rejected()
    {
        var result = TestManifests.Create(passengers: [new ManifestPassenger { Name = "  " }]);

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.PassengerNameRequired, result.Error);
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

    [Fact]
    public void Update_revises_passengers_and_cargo_and_raises_the_updated_event()
    {
        var manifest = TestManifests.Create().Value;
        manifest.ClearDomainEvents();

        var newPassengers = new List<ManifestPassenger>
        {
            new() { Name = "M. Beardy", BoardedOn = true },
            new() { Name = "J. Linklater", BoardedOn = true },
        };
        var newCargo = new List<ManifestCargoItem> { new() { Description = "Medical supplies", Secured = true } };

        var result = manifest.Update(
            manifest.TripDate,
            manifest.TripNumber,
            manifest.Route,
            manifest.Direction,
            manifest.Client,
            newPassengers,
            allSeatbeltsVerified: true,
            newCargo,
            CargoSecuredStatus.Yes,
            ManifestSource.Dispatcher,
            enteredBy: "Dispatch — R. Ballantyne");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, manifest.Passengers.Count);
        Assert.Single(manifest.Cargo);
        Assert.Equal(ManifestSource.Dispatcher, manifest.Source);
        Assert.Equal("Dispatch — R. Ballantyne", manifest.EnteredBy);
        Assert.NotNull(manifest.EnteredAt);

        var updated = Assert.IsType<TripManifestUpdatedDomainEvent>(Assert.Single(manifest.DomainEvents));
        Assert.Equal(manifest.Id, updated.ManifestId);
        Assert.Equal(ManifestSource.Dispatcher, updated.Source);
        Assert.Equal("Dispatch — R. Ballantyne", updated.EnteredBy);
    }

    [Fact]
    public void Update_by_dispatcher_without_entered_by_is_rejected()
    {
        var manifest = TestManifests.Create().Value;

        var result = manifest.Update(
            manifest.TripDate,
            manifest.TripNumber,
            manifest.Route,
            manifest.Direction,
            manifest.Client,
            manifest.Passengers,
            manifest.AllSeatbeltsVerified,
            manifest.Cargo,
            manifest.AllCargoSecured,
            ManifestSource.Dispatcher,
            enteredBy: null);

        Assert.True(result.IsFailure);
        Assert.Equal(TripManifestErrors.EnteredByRequired, result.Error);
    }
}
