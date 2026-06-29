using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ShuttleApi.IntegrationTests.Infrastructure;

namespace ShuttleApi.IntegrationTests.Trips;

public sealed class TripLifecycleTests(ShuttleWebFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task FullTripFlow_HappyPath_CompletesSuccessfully()
    {
        // 1. Setup
        var driverId = await CreateDriverAsync();
        var vehicleId = await CreateVehicleAsync();
        var tripId = await CreateAndDispatchTripAsync(driverId, vehicleId);

        // 2. Pre-inspection
        var preResp = await Client.PostAsJsonAsync(
            $"/api/trips/{tripId}/pre-inspection", StandardPreInspectionBody());
        preResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 3. Set En Route — first stop should be auto-marked arrived
        var enRouteResp = await Client.PutAsJsonAsync(
            $"/api/trips/{tripId}/status", new { status = "EnRoute" });
        enRouteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var trip = await GetTripAsync(tripId);
        trip.Status.Should().Be("EnRoute");
        trip.Stops.Should().HaveCount(2);
        var firstStop = trip.Stops.MinBy(s => s.SequenceOrder)!;
        firstStop.ArrivedAt.Should().NotBeNull("first stop is auto-arrived when going EnRoute");

        // 4. Depart first stop
        var departResp = await Client.PostAsync(
            $"/api/trips/{tripId}/stops/{firstStop.Id}/depart", null);
        departResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        trip = await GetTripAsync(tripId);
        trip.Stops.Single(s => s.Id == firstStop.Id).DepartedAt.Should().NotBeNull();

        // 5. Arrive at second (last) stop
        var lastStop = trip.Stops.MaxBy(s => s.SequenceOrder)!;
        var arriveLastResp = await Client.PostAsync(
            $"/api/trips/{tripId}/stops/{lastStop.Id}/arrive", null);
        arriveLastResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 6. Submit post-trip report — completes the trip
        var postResp = await Client.PostAsJsonAsync($"/api/trips/{tripId}/post-report", new
        {
            odometerEnd = 10250, fuelAddedLitres = (decimal?)null, fuelCostDollars = (decimal?)null,
            hasIncident = false, incidentType = (string?)null, incidentDescription = (string?)null,
            additionalNotes = (string?)null, isReadyToInvoice = true,
            exteriorNoNewDamage = true, interiorCleanedAndChecked = true,
            passengersDisembarkedSafely = true, allCargoDeliveredAndAccounted = true,
            vehicleSecuredAndPluggedIn = true, keysReturnedAndSecured = true
        });
        postResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        trip = await GetTripAsync(tripId);
        trip.Status.Should().Be("Completed");
        trip.PostReport.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkStopDeparted_WhenLastStop_Returns409()
    {
        var driverId = await CreateDriverAsync();
        var vehicleId = await CreateVehicleAsync();
        var tripId = await CreateAndDispatchTripAsync(driverId, vehicleId);

        await Client.PostAsJsonAsync($"/api/trips/{tripId}/pre-inspection", StandardPreInspectionBody());
        await Client.PutAsJsonAsync($"/api/trips/{tripId}/status", new { status = "EnRoute" });

        var trip = await GetTripAsync(tripId);
        var lastStop = trip.Stops.MaxBy(s => s.SequenceOrder)!;

        // Arrive at last stop first
        await Client.PostAsync($"/api/trips/{tripId}/stops/{lastStop.Id}/arrive", null);

        // Departing the last stop should be rejected
        var resp = await Client.PostAsync($"/api/trips/{tripId}/stops/{lastStop.Id}/depart", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task MarkStopArrived_WhenTripNotEnRoute_Returns409()
    {
        var driverId = await CreateDriverAsync();
        var vehicleId = await CreateVehicleAsync();
        var tripId = await CreateAndDispatchTripAsync(driverId, vehicleId);

        var trip = await GetTripAsync(tripId);
        var firstStop = trip.Stops.MinBy(s => s.SequenceOrder)!;

        // Trip is still Dispatched — marking arrived should fail
        var resp = await Client.PostAsync(
            $"/api/trips/{tripId}/stops/{firstStop.Id}/arrive", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<TripDto> GetTripAsync(string tripId)
    {
        var resp = await Client.GetAsync($"/api/trips/{tripId}");
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<TripDto>())!;
    }

    private sealed record TripDto(
        string Id,
        string Status,
        List<StopDto> Stops,
        object? PostReport);

    private sealed record StopDto(
        string Id,
        int SequenceOrder,
        DateTime? ArrivedAt,
        DateTime? DepartedAt);
}
