using System.Text.Json;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.IntegrationEvents.Compliance;
using Xunit;

namespace NorthernLink.Shared.Tests;

/// <summary>
/// <see cref="ComplianceRecordProducedIntegrationEvent.SupersedesSourceRecordId"/> was appended
/// after the event shipped (Epic 2). Rows already in drivers/fleet/trips outboxes were written
/// without it and must still bind — with the new member null — or the intake parks every one of
/// them as Failed (see <see cref="UserChangedIntegrationEventWireCompatTests"/>).
/// </summary>
public class ComplianceRecordProducedIntegrationEventWireCompatTests
{
    /// <summary>A verbatim pre-Epic-2 payload, camelCased as <see cref="IntegrationEventJson"/> writes it.</summary>
    private const string PreSupersessionPayload =
        """
        {
          "tenantId": "1c9f4a2e-5b3d-4e71-8c62-9d0a1b2c3d4e",
          "sourceModule": "fleet",
          "sourceRecordId": "inspection:7f3c",
          "recordType": "R9",
          "title": "Trip inspection — Unit 12 — 2026-09-14",
          "subjectKind": "Vehicle",
          "subjectId": "8f1d2c3b-4a5e-4f60-9a71-2b3c4d5e6f70",
          "subjectDisplay": "Unit 12",
          "anchorDates": { "RecordDate": "2026-09-14" },
          "supportingLinks": null,
          "regulatoryBasisKeys": ["NSC.13.16"],
          "printTemplateKey": null,
          "eventId": "aa11bb22-cc33-4d44-8e55-ff6677889900",
          "occurredAtUtc": "2026-10-11T16:40:51+00:00"
        }
        """;

    [Fact]
    public void A_payload_written_before_supersession_existed_still_deserializes_with_no_predecessor()
    {
        var deserialized = JsonSerializer.Deserialize<ComplianceRecordProducedIntegrationEvent>(
            PreSupersessionPayload, IntegrationEventJson.Options);

        Assert.NotNull(deserialized);
        Assert.Equal("inspection:7f3c", deserialized.SourceRecordId);
        Assert.Equal("R9", deserialized.RecordType);
        Assert.Equal(new DateOnly(2026, 9, 14), deserialized.AnchorDates!["RecordDate"]);
        Assert.Null(deserialized.SupersedesSourceRecordId);
    }

    [Fact]
    public void The_appended_member_round_trips_under_its_camel_case_name()
    {
        var original = new ComplianceRecordProducedIntegrationEvent(
            Guid.NewGuid(), "compliance", "insurance-policy:b", "R20", "Insurance", "Vehicle", Guid.NewGuid(), "Unit 101",
            null, null, null, null, SupersedesSourceRecordId: "insurance-policy:a");

        var json = IntegrationEventJson.Serialize(original);
        var back = JsonSerializer.Deserialize<ComplianceRecordProducedIntegrationEvent>(json, IntegrationEventJson.Options);

        Assert.Contains("\"supersedesSourceRecordId\":\"insurance-policy:a\"", json, StringComparison.Ordinal);
        Assert.Equal("insurance-policy:a", back!.SupersedesSourceRecordId);
    }
}
