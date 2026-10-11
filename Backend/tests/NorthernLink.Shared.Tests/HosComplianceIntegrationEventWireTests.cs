using System.Text.Json;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Drivers;
using Xunit;

namespace NorthernLink.Shared.Tests;

/// <summary>
/// The three compliance events the Drivers module publishes: their routing keys follow the
/// <c>drivers.&lt;kebab-name&gt;</c> convention, they round-trip on the shared wire format, and
/// the deferral-minutes field appended to the exception event defaults when absent.
/// </summary>
public class HosComplianceIntegrationEventWireTests
{
    [Theory]
    [InlineData(typeof(HosExceptionChangedIntegrationEvent), "drivers.hos-exception-changed")]
    [InlineData(typeof(DriverOutOfServiceChangedIntegrationEvent), "drivers.driver-out-of-service-changed")]
    [InlineData(typeof(EldMalfunctionChangedIntegrationEvent), "drivers.eld-malfunction-changed")]
    public void Routing_keys_follow_the_convention(Type eventType, string expected) =>
        Assert.Equal(expected, EventRoutingKey.For(eventType));

    [Fact]
    public void The_exception_event_round_trips_and_tolerates_a_payload_without_deferral_minutes()
    {
        var original = new HosExceptionChangedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Adverse driving conditions", "Declared",
            new DateTimeOffset(2026, 9, 14, 15, 0, 0, TimeSpan.Zero), null, Guid.NewGuid(), 90);

        var json = IntegrationEventJson.Serialize(original);
        Assert.Equal(original, JsonSerializer.Deserialize<HosExceptionChangedIntegrationEvent>(json, IntegrationEventJson.Options));
        Assert.Contains("\"kind\":\"Adverse driving conditions\"", json);

        const string withoutDeferral =
            """
            {
              "exceptionId": "4b7a1d2e-9c3f-4a5b-8d6e-7f8091a2b3c4",
              "tenantId": "1c9f4a2e-5b3d-4e71-8c62-9d0a1b2c3d4e",
              "driverId": "9f8e7d6c-5b4a-4392-8170-6f5e4d3c2b1a",
              "kind": "Emergency",
              "status": "Withdrawn",
              "effectiveFromUtc": "2026-09-14T15:00:00+00:00",
              "effectiveToUtc": null,
              "tripId": null,
              "extraDrivingMinutes": null,
              "eventId": "aa11bb22-cc33-4d44-8e55-ff6677889900",
              "occurredAtUtc": "2026-09-14T15:00:52+00:00"
            }
            """;
        var legacy = JsonSerializer.Deserialize<HosExceptionChangedIntegrationEvent>(withoutDeferral, IntegrationEventJson.Options);
        Assert.NotNull(legacy);
        Assert.Equal("Withdrawn", legacy.Status);
        Assert.Null(legacy.DeferredOffDutyMinutes);
    }

    [Fact]
    public void The_out_of_service_and_eld_events_round_trip()
    {
        var oos = new DriverOutOfServiceChangedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Active",
            new DateTimeOffset(2026, 9, 14, 19, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 17, 19, 0, 0, TimeSpan.Zero), "Inspection");
        Assert.Equal(oos, JsonSerializer.Deserialize<DriverOutOfServiceChangedIntegrationEvent>(IntegrationEventJson.Serialize(oos), IntegrationEventJson.Options));

        var eld = new EldMalfunctionChangedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "J. Spence", "P", "Open",
            new DateTimeOffset(2026, 9, 14, 13, 0, 0, TimeSpan.Zero), "Positioning malfunction");
        Assert.Equal(eld, JsonSerializer.Deserialize<EldMalfunctionChangedIntegrationEvent>(IntegrationEventJson.Serialize(eld), IntegrationEventJson.Options));
    }
}
