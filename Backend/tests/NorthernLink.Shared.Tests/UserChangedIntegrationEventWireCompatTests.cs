using System.Text.Json;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Identity;
using Xunit;

namespace NorthernLink.Shared.Tests;

/// <summary>
/// An outbox payload is stored as text and deserialized later, possibly much later, by whatever
/// binary happens to be running then. So the shape a producer wrote is a contract with the
/// future, not just with today's consumer.
/// <para>
/// These tests pin the one property that makes appending a field to
/// <see cref="UserChangedIntegrationEvent"/> safe: a payload written before the field existed
/// still deserializes, with the new member null. Break it — by marking a member
/// <c>required</c> or <c>[JsonRequired]</c> — and every historical row throws
/// <see cref="JsonException"/> on read, which <c>OutboxPollingConsumer</c> treats as permanently
/// broken and parks as Failed immediately, with no retry and no compile error anywhere to warn
/// you first.
/// </para>
/// </summary>
public class UserChangedIntegrationEventWireCompatTests
{
    /// <summary>
    /// A verbatim pre-profile payload: the four fields the event carried before it grew
    /// <c>fullName</c>/<c>jobTitle</c>, camelCased exactly as <see cref="IntegrationEventJson"/>
    /// writes them. Deliberately a literal rather than something re-serialized from the current
    /// type — a round-trip through today's record would prove nothing about yesterday's rows.
    /// </summary>
    private const string PreProfilePayload =
        """
        {
          "userId": "8f1d2c3b-4a5e-4f60-9a71-2b3c4d5e6f70",
          "tenantId": "1c9f4a2e-5b3d-4e71-8c62-9d0a1b2c3d4e",
          "email": "planner@northernlink.ca",
          "role": "Accountant",
          "eventId": "aa11bb22-cc33-4d44-8e55-ff6677889900",
          "occurredAtUtc": "2026-08-12T16:40:51+00:00"
        }
        """;

    [Fact]
    public void A_payload_written_before_profiles_existed_still_deserializes()
    {
        var deserialized = JsonSerializer.Deserialize<UserChangedIntegrationEvent>(
            PreProfilePayload, IntegrationEventJson.Options);

        Assert.NotNull(deserialized);
        Assert.Equal(Guid.Parse("8f1d2c3b-4a5e-4f60-9a71-2b3c4d5e6f70"), deserialized.UserId);
        Assert.Equal(Guid.Parse("1c9f4a2e-5b3d-4e71-8c62-9d0a1b2c3d4e"), deserialized.TenantId);
        Assert.Equal("planner@northernlink.ca", deserialized.Email);
        Assert.Equal("Accountant", deserialized.Role);
    }

    [Fact]
    public void The_absent_profile_fields_bind_to_null_not_to_empty_strings()
    {
        // The consumer's fallback to email keys on null. Were these to bind as "", a legacy row
        // would replay as a user whose name is blank rather than absent.
        var deserialized = JsonSerializer.Deserialize<UserChangedIntegrationEvent>(
            PreProfilePayload, IntegrationEventJson.Options);

        Assert.Null(deserialized!.FullName);
        Assert.Null(deserialized.JobTitle);
    }

    [Fact]
    public void The_base_records_event_id_and_timestamp_survive_the_round_trip()
    {
        // EventId is the consumer's idempotency key under at-least-once delivery — losing it on
        // a replayed legacy row would mean reprocessing something already handled.
        var deserialized = JsonSerializer.Deserialize<UserChangedIntegrationEvent>(
            PreProfilePayload, IntegrationEventJson.Options);

        Assert.Equal(Guid.Parse("aa11bb22-cc33-4d44-8e55-ff6677889900"), deserialized!.EventId);
        Assert.Equal(
            DateTimeOffset.Parse("2026-08-12T16:40:51+00:00"), deserialized.OccurredAtUtc);
    }

    [Fact]
    public void A_current_payload_round_trips_with_the_profile_intact()
    {
        var original = new UserChangedIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "planner@northernlink.ca",
            "Accountant",
            "Léa Fontaine",
            "Financial Planner");

        var json = IntegrationEventJson.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<UserChangedIntegrationEvent>(
            json, IntegrationEventJson.Options);

        Assert.Equal(original, deserialized);
    }
}
