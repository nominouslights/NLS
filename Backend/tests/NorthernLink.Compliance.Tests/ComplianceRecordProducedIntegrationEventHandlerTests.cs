using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Compliance.Application.Integration;
using NorthernLink.Compliance.Application.Records;
using NorthernLink.Compliance.Application.Records.Register;
using NorthernLink.Shared.IntegrationEvents.Compliance;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// The registry's intake. Outbox delivery is at-least-once and subscribing replays a routing key's
/// whole history, so "the same event twice" is the normal case — these tests pin convergence, and
/// that a producer bug (unknown type) parks visibly rather than registering nonsense.
/// </summary>
public class ComplianceRecordProducedIntegrationEventHandlerTests
{
    private readonly InMemoryComplianceStore _store = new();
    private readonly ComplianceRecordProducedIntegrationEventHandler _handler;
    private readonly RoutingSender _sender;

    public ComplianceRecordProducedIntegrationEventHandlerTests()
    {
        _sender = new RoutingSender(new RegisterRecordCommandHandler(new RecordRegistrar(_store, _store, _store)));
        _handler = new ComplianceRecordProducedIntegrationEventHandler(_sender, NullLogger<ComplianceRecordProducedIntegrationEventHandler>.Instance);
    }

    private static ComplianceRecordProducedIntegrationEvent Event(
        string sourceRecordId = "inspection:7f3c",
        string recordType = "R9",
        string subjectKind = "Vehicle",
        IReadOnlyDictionary<string, DateOnly>? anchors = null) =>
        new(
            TestCompliance.TenantId,
            "fleet",
            sourceRecordId,
            recordType,
            "Trip inspection — Unit 12 — 2026-09-14",
            subjectKind,
            TestCompliance.VehicleId,
            "Unit 12 (2019 Ford Transit)",
            anchors ?? new Dictionary<string, DateOnly> { ["RecordDate"] = new DateOnly(2026, 9, 14) },
            [new ComplianceSupportingLinkPayload("fleet-inspection", "7f3c", "Inspection report")],
            ["MR.95-2008.12.1"],
            null);

    [Fact]
    public async Task A_first_event_registers_the_record_under_the_events_tenant()
    {
        await _handler.Handle(Event(), CancellationToken.None);

        var record = Assert.Single(_store.Records);
        Assert.Equal(TestCompliance.TenantId, record.TenantId);
        Assert.Equal("fleet", record.SourceModule);
        Assert.Equal("inspection:7f3c", record.SourceRecordId);
        Assert.Equal(new DateOnly(2027, 3, 30), record.RetainUntil);
        Assert.Single(record.SupportingLinks);
        Assert.Contains("MR.95-2008.12.1", record.RegulatoryBasisKeys);
        Assert.Equal(TestCompliance.TenantId, _sender.AmbientTenantAtSend);
    }

    [Fact]
    public async Task Replaying_the_same_event_leaves_exactly_one_record()
    {
        var integrationEvent = Event();

        await _handler.Handle(integrationEvent, CancellationToken.None);
        await _handler.Handle(integrationEvent, CancellationToken.None);

        Assert.Single(_store.Records);
    }

    [Fact]
    public async Task Two_producer_records_become_two_registry_records()
    {
        await _handler.Handle(Event("inspection:a"), CancellationToken.None);
        await _handler.Handle(Event("inspection:b"), CancellationToken.None);

        Assert.Equal(2, _store.Records.Count);
    }

    [Theory]
    [InlineData("R99", "Vehicle", "RecordDate")]
    [InlineData("R9", "Spaceship", "RecordDate")]
    [InlineData("R9", "Vehicle", "WhenItRained")]
    public async Task A_producer_bug_throws_so_the_consumer_parks_the_row_visibly(string recordType, string subjectKind, string anchorName)
    {
        var integrationEvent = Event(recordType: recordType, subjectKind: subjectKind,
            anchors: new Dictionary<string, DateOnly> { [anchorName] = new DateOnly(2026, 9, 14) });

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(integrationEvent, CancellationToken.None));
        Assert.Empty(_store.Records);
    }

    [Fact]
    public async Task A_payload_with_null_collections_still_registers()
    {
        var sparse = new ComplianceRecordProducedIntegrationEvent(
            TestCompliance.TenantId, "drivers", "abstract:1", "R14", "Driver abstract review — J. Smith", "Driver", Guid.NewGuid(), "J. Smith",
            new Dictionary<string, DateOnly> { ["RecordDate"] = new DateOnly(2026, 5, 5) }, null, null, null);

        await _handler.Handle(sparse, CancellationToken.None);

        var record = Assert.Single(_store.Records);
        Assert.Empty(record.SupportingLinks);
        Assert.Equal(new DateOnly(2028, 5, 5), record.RetainUntil);
    }

    /// <summary>Routes RegisterRecordCommand to the real handler and records the ambient tenant at dispatch.</summary>
    private sealed class RoutingSender(RegisterRecordCommandHandler register) : ISender
    {
        public Guid? AmbientTenantAtSend { get; private set; }

        public Task<Result> Send(ICommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            AmbientTenantAtSend = AmbientTenant.Current;
            var result = await register.Handle((RegisterRecordCommand)(object)command, cancellationToken);
            return (Result<TResponse>)(object)result;
        }

        public Task<Result<TResponse>> Query<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
