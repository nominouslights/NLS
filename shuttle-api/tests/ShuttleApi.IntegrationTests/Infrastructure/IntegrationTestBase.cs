using System.Net.Http.Json;

namespace ShuttleApi.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase : IClassFixture<ShuttleWebFactory>, IAsyncLifetime
{
    protected readonly HttpClient Client;
    private readonly DatabaseCleaner _cleaner;

    protected IntegrationTestBase(ShuttleWebFactory factory)
    {
        _cleaner = new DatabaseCleaner(factory);
        // Client is authenticated after InitializeAsync
        Client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _cleaner.CleanAsync();

        // Obtain admin token and set as default header for this test class
        var loginResp = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@northernlink.com",
            password = "Admin123!"
        });
        loginResp.EnsureSuccessStatusCode();
        var body = await loginResp.Content.ReadFromJsonAsync<TokenBody>();
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", body!.accessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Shared setup helpers ──────────────────────────────────────────────────

    protected async Task<string> CreateDriverAsync()
    {
        var resp = await Client.PostAsJsonAsync("/api/drivers", new
        {
            firstName = "Test", lastName = "Driver", email = "driver@test.com",
            phone = "555-0001", employeeId = "EMP-TEST-1",
            licenseNumber = "DL-TEST-1", licenseExpiry = "2028-01-01T00:00:00Z"
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<IdBody>();
        return body!.id!;
    }

    protected async Task<string> CreateVehicleAsync()
    {
        var resp = await Client.PostAsJsonAsync("/api/vehicles", new
        {
            unitCode = "V-TEST-1", make = "Ford", model = "Transit", year = 2023,
            licensePlate = "TEST-001", color = "White", capacity = 15,
            vin = "1FTBR1Y8TEST00001", province = "MB"
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<IdBody>();
        return body!.id!;
    }

    protected async Task<string> CreateAndDispatchTripAsync(string driverId, string vehicleId)
    {
        // Create
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
        var tripId = created!.id!;

        // Assign driver
        var assignResp = await Client.PutAsJsonAsync($"/api/trips/{tripId}/assign-driver",
            new { driverId, vehicleType = "Transit Van" });
        assignResp.EnsureSuccessStatusCode();

        // Dispatch
        var dispatchResp = await Client.PostAsync($"/api/trips/{tripId}/dispatch",
            JsonContent.Create(new { }));
        dispatchResp.EnsureSuccessStatusCode();

        return tripId;
    }

    protected static object StandardPreInspectionBody() => new
    {
        odometerStart = 10000,
        fuelLevel = "Full",
        weatherType = "Clear",
        temperature = "10C",
        roadConditions = "Dry",
        visibility = "Good",
        roadAdvisories = (string?)null,
        weatherPulledAt = (DateTime?)null,
        items = new[]
        {
            new { itemName = "Tires", category = "ExteriorMechanical", passed = true, notes = (string?)null },
            new { itemName = "Lights", category = "ExteriorMechanical", passed = true, notes = (string?)null },
            new { itemName = "Seatbelts", category = "SafetyEquipment", passed = true, notes = (string?)null }
        }
    };

    private sealed record TokenBody(string accessToken, string refreshToken);
    private sealed record IdBody(string? id);
}
