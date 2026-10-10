using System.Text.Json;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Drivers;
using Xunit;

namespace NorthernLink.Shared.Tests;

/// <summary>
/// The same contract-with-the-past guarantee <see cref="UserChangedIntegrationEventWireCompatTests"/>
/// pins, for <see cref="DriverChangedIntegrationEvent"/> after it grew <c>userId</c>: every
/// <c>drivers.outbox_messages</c> row written before the field existed must still deserialize,
/// with the member null — otherwise the Trips consumer parks the whole routing key as Failed on
/// its first replay.
/// </summary>
public class DriverChangedIntegrationEventWireCompatTests
{
    /// <summary>A verbatim pre-userId payload, camelCased as <see cref="IntegrationEventJson"/> writes it.</summary>
    private const string PreUserIdPayload =
        """
        {
          "driverId": "4b7a1d2e-9c3f-4a5b-8d6e-7f8091a2b3c4",
          "tenantId": "1c9f4a2e-5b3d-4e71-8c62-9d0a1b2c3d4e",
          "name": "R. Ballantyne",
          "licenceClass": "Class 4",
          "status": "Active",
          "source": "Northern Link",
          "eventId": "aa11bb22-cc33-4d44-8e55-ff6677889900",
          "occurredAtUtc": "2026-07-19T09:40:53+00:00"
        }
        """;

    [Fact]
    public void A_payload_written_before_user_id_existed_still_deserializes_with_it_null()
    {
        var deserialized = JsonSerializer.Deserialize<DriverChangedIntegrationEvent>(
            PreUserIdPayload, IntegrationEventJson.Options);

        Assert.NotNull(deserialized);
        Assert.Equal(Guid.Parse("4b7a1d2e-9c3f-4a5b-8d6e-7f8091a2b3c4"), deserialized.DriverId);
        Assert.Equal("Active", deserialized.Status);
        Assert.Null(deserialized.UserId);
        Assert.Equal(Guid.Parse("aa11bb22-cc33-4d44-8e55-ff6677889900"), deserialized.EventId);
    }

    [Fact]
    public void A_current_payload_round_trips_with_the_user_id_intact()
    {
        var original = new DriverChangedIntegrationEvent(
            Guid.NewGuid(), Guid.NewGuid(), "R. Ballantyne", "Class 4", "Active", "Northern Link", Guid.NewGuid());

        var json = IntegrationEventJson.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<DriverChangedIntegrationEvent>(json, IntegrationEventJson.Options);

        Assert.Equal(original, deserialized);
    }
}
