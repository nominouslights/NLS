using System.Text.Json;
using NorthernLink.Fleet.Application.Inspections;
using NorthernLink.Fleet.Application.Inspections.Enter;
using NorthernLink.Fleet.Application.Inspections.Update;
using NorthernLink.Fleet.Domain.Inspections;
using NorthernLink.Fleet.Infrastructure.Endpoints;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// The inspection Location — the "urban municipality or description of the highway location"
/// that Manitoba's Commercial Vehicle Trip Inspection Regulation (M.R. 95/2008 s.12(1), NSC
/// Standard 13) requires on every trip-inspection report. Optional server-side on purpose
/// (paper-form back-entries lack it), trimmed, whitespace-only stored as null, capped at
/// <see cref="VehicleInspection.LocationMaxLength"/>, amendable, and out of reach of the carrier
/// acknowledgement.
/// </summary>
public class InspectionLocationTests
{
    [Fact]
    public void Enter_trims_the_location()
    {
        var inspection = TestInspections.PreTrip(location: "  Thompson, MB  ");

        Assert.Equal("Thompson, MB", inspection.Location);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n ")]
    public void Enter_stores_a_missing_or_whitespace_location_as_null(string? location)
    {
        // Not required: a record keyed in from an old paper form legitimately has none.
        var inspection = TestInspections.PreTrip(location: location);

        Assert.Null(inspection.Location);
    }

    [Fact]
    public void Enter_accepts_a_location_of_exactly_the_maximum_length()
    {
        var atLimit = new string('x', VehicleInspection.LocationMaxLength);

        var inspection = TestInspections.PreTrip(location: atLimit);

        Assert.Equal(atLimit, inspection.Location);
    }

    [Fact]
    public void The_maximum_length_is_200()
    {
        // Pinned separately: the column is varchar(200) and the frontends cap their input at 200.
        Assert.Equal(200, VehicleInspection.LocationMaxLength);
    }

    [Fact]
    public void Enter_rejects_a_location_over_the_maximum_length()
    {
        var result = EnterPreTrip(new string('x', VehicleInspection.LocationMaxLength + 1));

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.LocationTooLong, result.Error);
    }

    [Fact]
    public void The_limit_is_measured_after_trimming()
    {
        var padded = "   " + new string('x', VehicleInspection.LocationMaxLength) + "   ";

        var result = EnterPreTrip(padded);

        Assert.True(result.IsSuccess);
        Assert.Equal(VehicleInspection.LocationMaxLength, result.Value.Location!.Length);
    }

    [Fact]
    public void Amend_changes_the_location()
    {
        var inspection = TestInspections.PreTrip(location: "Thompson, MB");

        var result = TestInspections.AmendWith(inspection, defects: [], location: "  PTH 6, km 40 south of Grand Rapids ");

        Assert.True(result.IsSuccess);
        Assert.Equal("PTH 6, km 40 south of Grand Rapids", inspection.Location);
    }

    [Fact]
    public void Amend_can_clear_the_location()
    {
        var inspection = TestInspections.PreTrip(location: "Thompson, MB");

        var result = TestInspections.AmendWith(inspection, defects: [], location: "   ");

        Assert.True(result.IsSuccess);
        Assert.Null(inspection.Location);
    }

    [Fact]
    public void Amend_rejects_an_over_long_location_and_leaves_the_record_untouched()
    {
        var inspection = TestInspections.PreTrip(location: "Thompson, MB");
        var odometerBefore = inspection.OdometerKm;

        var result = TestInspections.AmendWith(
            inspection,
            defects: [],
            odometerKm: 999_999,
            location: new string('x', VehicleInspection.LocationMaxLength + 1));

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.LocationTooLong, result.Error);
        Assert.Equal("Thompson, MB", inspection.Location);
        Assert.Equal(odometerBefore, inspection.OdometerKm);
    }

    [Fact]
    public void Acknowledging_as_carrier_does_not_touch_the_location()
    {
        var inspection = TestInspections.PreTrip(
            defects: [TestInspections.Defect(InspectionDefectSeverity.Major)],
            location: "Thompson, MB");

        var result = inspection.AcknowledgeAsCarrier("R. Beardy", "Unit parked", DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal("Thompson, MB", inspection.Location);
    }

    [Fact]
    public void Acknowledging_a_report_without_a_location_leaves_it_null()
    {
        var inspection = TestInspections.PreTrip(defects: [TestInspections.Defect(InspectionDefectSeverity.Major)]);

        Assert.True(inspection.AcknowledgeAsCarrier("R. Beardy", note: null, DateTimeOffset.UtcNow).IsSuccess);

        Assert.Null(inspection.Location);
    }

    [Fact]
    public void The_carrier_acknowledgement_takes_no_location_parameter()
    {
        // Structural guard: the acknowledgement must never become a second way to write the
        // report body.
        var parameters = typeof(VehicleInspection)
            .GetMethod(nameof(VehicleInspection.AcknowledgeAsCarrier))!
            .GetParameters();

        Assert.DoesNotContain(parameters, p => p.Name!.Contains("location", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_enter_handler_carries_the_location_through()
    {
        var repository = new InMemoryVehicleInspectionRepository();
        var handler = new EnterInspectionCommandHandler(repository);

        var result = await handler.Handle(
            EnterCommand(source: InspectionSource.DriverApp, location: " Leaf Rapids "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Leaf Rapids", Assert.Single(repository.Inspections).Location);
    }

    [Fact]
    public async Task The_update_handler_carries_the_location_through()
    {
        var repository = new InMemoryVehicleInspectionRepository();
        var stored = TestInspections.PreTrip(location: "Thompson, MB");
        repository.Add(stored);
        var handler = new UpdateInspectionCommandHandler(repository, new InMemoryWorkOrderRepository());

        var result = await handler.Handle(UpdateCommand(stored.Id, location: "Lynn Lake"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Lynn Lake", stored.Location);
    }

    [Fact]
    public void The_request_body_binds_location_from_json()
    {
        // POST /api/fleet/inspections (Dispatcher create AND Driver Field App submission) and
        // PUT /api/fleet/inspections/{id} (amend) all bind this one record.
        var request = JsonSerializer.Deserialize<InspectionRequest>(
            """{ "unit": "U-04", "driverName": "J. Spence", "location": "Thompson, MB" }""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(request);
        Assert.Equal("Thompson, MB", request.Location);
    }

    [Fact]
    public void The_request_body_still_binds_without_location()
    {
        var request = JsonSerializer.Deserialize<InspectionRequest>(
            """{ "unit": "U-04", "driverName": "J. Spence" }""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(request);
        Assert.Null(request.Location);
    }

    [Fact]
    public void The_response_wire_carries_location()
    {
        var response = VehicleInspectionResponseMapper.ToResponse(TestInspections.PreTrip(location: "Thompson, MB"));

        using var json = JsonDocument.Parse(
            JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.True(json.RootElement.TryGetProperty("location", out var location));
        Assert.Equal("Thompson, MB", location.GetString());
    }

    [Fact]
    public void The_response_wire_carries_location_as_null_when_absent()
    {
        var response = VehicleInspectionResponseMapper.ToResponse(TestInspections.PreTrip());

        using var json = JsonDocument.Parse(
            JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.True(json.RootElement.TryGetProperty("location", out var location));
        Assert.Equal(JsonValueKind.Null, location.ValueKind);
    }

    private static NorthernLink.Shared.Kernel.Result<VehicleInspection> EnterPreTrip(string? location) =>
        VehicleInspection.Enter(
            TestVehicles.TenantId,
            InspectionSource.Dispatcher,
            InspectionType.PreTrip,
            tripNumber: "TR-4818",
            vehicleId: null,
            unit: "U-04",
            driverName: "J. Spence",
            enteredBy: null,
            performedAt: DateTimeOffset.UtcNow,
            odometerKm: 118_204,
            checklistItems: [],
            defects: [],
            weather: [],
            temperatureC: null,
            roadConditions: [],
            visibility: null,
            roadAdvisories: null,
            fuelLevel: null,
            issues: [],
            attestations: [],
            driverSignatureName: null,
            certifiedAt: null,
            fuelAdded: false,
            fuelLitres: null,
            fuelCostCad: null,
            certificationStatement: null,
            location: location);

    private static EnterInspectionCommand EnterCommand(InspectionSource source, string? location) =>
        new(
            TestVehicles.TenantId,
            source,
            InspectionType.PreTrip,
            TripNumber: "TR-4818",
            VehicleId: null,
            Unit: "U-04",
            DriverName: "J. Spence",
            EnteredBy: null,
            PerformedAt: DateTimeOffset.UtcNow,
            OdometerKm: 118_204,
            Checklist: [],
            Defects: [],
            Weather: [],
            TemperatureC: null,
            RoadConditions: [],
            Visibility: null,
            RoadAdvisories: null,
            FuelLevel: null,
            Issues: [],
            Attestations: [],
            DriverSignatureName: null,
            CertifiedAt: null,
            FuelAdded: false,
            FuelLitres: null,
            FuelCostCad: null,
            Location: location);

    private static UpdateInspectionCommand UpdateCommand(Guid inspectionId, string? location) =>
        new(
            inspectionId,
            InspectionSource.Dispatcher,
            VehicleId: null,
            Unit: "U-04",
            DriverName: "J. Spence",
            EnteredBy: null,
            PerformedAt: DateTimeOffset.UtcNow,
            OdometerKm: 118_500,
            Checklist: [],
            Defects: [],
            Weather: [],
            TemperatureC: null,
            RoadConditions: [],
            Visibility: null,
            RoadAdvisories: null,
            FuelLevel: null,
            Issues: [],
            Attestations: [],
            DriverSignatureName: null,
            CertifiedAt: null,
            FuelAdded: false,
            FuelLitres: null,
            FuelCostCad: null,
            Location: location);
}
