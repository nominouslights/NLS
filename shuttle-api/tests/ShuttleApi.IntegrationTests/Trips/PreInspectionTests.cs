using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ShuttleApi.IntegrationTests.Infrastructure;

namespace ShuttleApi.IntegrationTests.Trips;

public sealed class PreInspectionTests(ShuttleWebFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task SubmitPreInspection_WhenTripIsDispatched_Returns204()
    {
        var driverId = await CreateDriverAsync();
        var vehicleId = await CreateVehicleAsync();
        var tripId = await CreateAndDispatchTripAsync(driverId, vehicleId);

        var resp = await Client.PostAsJsonAsync(
            $"/api/trips/{tripId}/pre-inspection", StandardPreInspectionBody());

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SubmitPreInspection_WhenAlreadySubmitted_Returns409()
    {
        var driverId = await CreateDriverAsync();
        var vehicleId = await CreateVehicleAsync();
        var tripId = await CreateAndDispatchTripAsync(driverId, vehicleId);

        // First submission succeeds
        var first = await Client.PostAsJsonAsync(
            $"/api/trips/{tripId}/pre-inspection", StandardPreInspectionBody());
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Second submission on the same trip is a conflict
        var second = await Client.PostAsJsonAsync(
            $"/api/trips/{tripId}/pre-inspection", StandardPreInspectionBody());

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await second.Content.ReadFromJsonAsync<ErrorBody>();
        body!.Error.Should().Be("A pre-trip inspection has already been submitted for this trip.");
    }

    [Fact]
    public async Task SubmitPreInspection_WhenTripNotDispatched_Returns409()
    {
        // Create a trip but do NOT dispatch it
        var driverId = await CreateDriverAsync();
        var vehicleId = await CreateVehicleAsync();

        var createResp = await Client.PostAsJsonAsync("/api/trips", new
        {
            serviceType = "Community", vehicleId, vehicleType = "Transit Van",
            scheduledAt = DateTime.UtcNow.AddDays(1).ToString("o"),
            stops = new[]
            {
                new { sequenceOrder = 1, locationName = "Thompson", address = "123 Main St" },
                new { sequenceOrder = 2, locationName = "Lynn Lake", address = "456 Main St" }
            },
            isDeadhead = true, isDeadheadBillable = false
        });
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<IdBody>();

        var resp = await Client.PostAsJsonAsync(
            $"/api/trips/{created!.Id}/pre-inspection", StandardPreInspectionBody());

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SetEnRoute_WhenPreInspectionMissing_Returns409()
    {
        var driverId = await CreateDriverAsync();
        var vehicleId = await CreateVehicleAsync();
        var tripId = await CreateAndDispatchTripAsync(driverId, vehicleId);

        // Skip pre-inspection and try to go EnRoute
        var resp = await Client.PutAsJsonAsync(
            $"/api/trips/{tripId}/status", new { status = "EnRoute" });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await resp.Content.ReadFromJsonAsync<ErrorBody>();
        body!.Error.Should().Be("Pre-trip inspection must be completed before the trip can go en route.");
    }

    private sealed record ErrorBody(string Error);
    private sealed record IdBody(string Id);
}
