using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Compliance.Application;
using NorthernLink.Compliance.Application.Authority;
using NorthernLink.Compliance.Application.Insurance;
using NorthernLink.Compliance.Application.Integration;
using NorthernLink.Compliance.Application.Records;
using NorthernLink.Compliance.Application.Records.Register;
using NorthernLink.Compliance.Application.RuleSets;
using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Compliance.Domain.Insurance;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.IntegrationEvents.Compliance;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// Compliance registering its own R20 documents end to end, with the in-memory store standing in
/// for both outbox hops: an aggregate's saved domain events go through the real mapper, the
/// produced events through the real intake handler (registrar + supersession), and the registered
/// answers through the real stamp handler back onto the aggregate.
/// </summary>
public class AuthorityRecordRoundTripTests
{
    private readonly InMemoryComplianceStore _store = new();
    private readonly InMemoryLookupStore _lookups = new();
    private readonly ComplianceIntegrationEventMapper _mapper = new();
    private readonly ComplianceRecordProducedIntegrationEventHandler _intake;
    private readonly ComplianceRecordRegisteredIntegrationEventHandler _stamp;
    private int _deliveredEvents;

    public AuthorityRecordRoundTripTests()
    {
        _intake = new ComplianceRecordProducedIntegrationEventHandler(
            new RegisterSender(new RegisterRecordCommandHandler(new RecordRegistrar(_store, _store, _store))),
            NullLogger<ComplianceRecordProducedIntegrationEventHandler>.Instance);
        _stamp = new ComplianceRecordRegisteredIntegrationEventHandler(
            [new SfcRecordStampTarget(_store, _store), new InsurancePolicyRecordStampTarget(_store, _store), new VehicleRegistrationRecordStampTarget(_store, _store)],
            NullLogger<ComplianceRecordRegisteredIntegrationEventHandler>.Instance);
    }

    /// <summary>
    /// Delivers every event saved since the last call: the mapper turns domain events into
    /// integration events (the outbox), produced ones go to the intake and registered ones to the
    /// stamp handler — repeated until the outbox is quiet, as the poller would.
    /// </summary>
    private async Task<List<ComplianceRecordProducedIntegrationEvent>> DeliverAsync()
    {
        var produced = new List<ComplianceRecordProducedIntegrationEvent>();
        while (_deliveredEvents < _store.SavedEvents.Count)
        {
            var domainEvent = _store.SavedEvents[_deliveredEvents++];
            switch (_mapper.Map(domainEvent, aggregate: null!))
            {
                case ComplianceRecordProducedIntegrationEvent p:
                    produced.Add(p);
                    await _intake.Handle(p, CancellationToken.None);
                    break;
                case ComplianceRecordRegisteredIntegrationEvent r:
                    await _stamp.Handle(r, CancellationToken.None);
                    break;
            }
        }

        return produced;
    }

    private Task RecordSfc(string number, DateOnly issued, DateOnly expires) =>
        new RecordSfcCommandHandler(_store, _store).Handle(
            new RecordSfcCommand(TestCompliance.TenantId, number, issued, expires, TestCompliance.ActorId), CancellationToken.None);

    [Fact]
    public async Task An_SFC_is_registered_as_an_R20_record_and_its_number_is_stamped_back()
    {
        await RecordSfc("MB-SFC-001", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1));

        var produced = Assert.Single(await DeliverAsync());

        Assert.Equal(ComplianceSourceIds.Module, produced.SourceModule);
        Assert.Equal("R20", produced.RecordType);
        Assert.Equal("Carrier", produced.SubjectKind);
        Assert.Null(produced.SupersedesSourceRecordId);

        var record = Assert.Single(_store.Records);
        Assert.Equal(ComplianceRecordType.R20, record.RecordType);
        Assert.True(record.RetentionPending); // R20 counts from supersession — pending until renewed
        var current = Assert.Single(_store.Authorities).CurrentSfc!;
        Assert.Equal(record.Id, current.ComplianceRecordId);
        Assert.Equal(record.DocumentNumber, current.DocumentNumber);
        Assert.StartsWith("NL-SP-R20-", record.DocumentNumber);
    }

    [Fact]
    public async Task A_new_SFC_supersedes_the_previous_record_which_starts_its_two_year_clock()
    {
        await RecordSfc("MB-SFC-001", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1));
        await DeliverAsync();
        var firstRecord = Assert.Single(_store.Records);

        await RecordSfc("MB-SFC-002", new DateOnly(2027, 9, 20), new DateOnly(2028, 9, 20));
        var produced = Assert.Single(await DeliverAsync());

        Assert.Equal(firstRecord.SourceRecordId, produced.SupersedesSourceRecordId);
        Assert.Equal(2, _store.Records.Count);
        Assert.Equal(ComplianceRecordStatus.Superseded, firstRecord.Status);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(today.AddMonths(24), firstRecord.RetainUntil);

        var successor = _store.Records.Single(r => r.Id != firstRecord.Id);
        Assert.Equal(firstRecord.Id, successor.SupersedesId);
        Assert.Equal(2, successor.DocumentVersion);
        Assert.Equal(ComplianceRecordStatus.Active, successor.Status);

        // Both certificates carry their own record's number.
        var authority = Assert.Single(_store.Authorities);
        Assert.Equal(successor.DocumentNumber, authority.CurrentSfc!.DocumentNumber);
        Assert.Equal(firstRecord.DocumentNumber, Assert.Single(authority.SfcHistory).DocumentNumber);
    }

    [Fact]
    public async Task Replaying_a_superseding_event_changes_nothing()
    {
        await RecordSfc("MB-SFC-001", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1));
        await DeliverAsync();
        await RecordSfc("MB-SFC-002", new DateOnly(2027, 9, 20), new DateOnly(2028, 9, 20));
        var produced = Assert.Single(await DeliverAsync());

        await _intake.Handle(produced, CancellationToken.None);

        Assert.Equal(2, _store.Records.Count);
        Assert.Single(_store.Records, r => r.Status == ComplianceRecordStatus.Active);
    }

    [Fact]
    public async Task A_predecessor_that_was_never_registered_leaves_a_fresh_registration()
    {
        var orphan = new ComplianceRecordProducedIntegrationEvent(
            TestCompliance.TenantId, "compliance", "insurance-policy:" + Guid.NewGuid().ToString("N"), "R20", "Insurance", "Vehicle",
            TestCompliance.VehicleId, "Unit 101", new Dictionary<string, DateOnly> { ["RecordDate"] = new DateOnly(2026, 10, 1) },
            null, null, null, SupersedesSourceRecordId: "insurance-policy:never-seen");

        await _intake.Handle(orphan, CancellationToken.None);

        var record = Assert.Single(_store.Records);
        Assert.Equal(1, record.DocumentVersion);
        Assert.Null(record.SupersedesId);
    }

    [Fact]
    public async Task A_renewed_insurance_policy_supersedes_its_record_and_both_policies_are_stamped()
    {
        _lookups.AddVehicle(TestCompliance.VehicleId, unit: "101");
        var rules = new ActiveRuleSetResolver(new InMemoryReferenceStore(), new InMemoryRuleSetApplicationRepository());
        var details = new InsurancePolicyDetails
        {
            PolicyNumber = "MPI-1", LiabilityLimitCad = 2_000_000m, EffectiveOn = new DateOnly(2026, 10, 1), ExpiresOn = new DateOnly(2027, 10, 1),
        };

        var first = await new RecordInsurancePolicyCommandHandler(_store, _lookups, rules, _store)
            .Handle(new RecordInsurancePolicyCommand(TestCompliance.TenantId, TestCompliance.VehicleId, details, null), CancellationToken.None);
        await DeliverAsync();
        var second = await new RenewInsurancePolicyCommandHandler(_store, rules, _store)
            .Handle(new RenewInsurancePolicyCommand(TestCompliance.TenantId, first.Value, details with { PolicyNumber = "MPI-2" }, null), CancellationToken.None);
        var produced = Assert.Single(await DeliverAsync());

        Assert.Equal(ComplianceSourceIds.InsurancePolicy(first.Value), produced.SupersedesSourceRecordId);
        Assert.Equal("Vehicle", produced.SubjectKind);
        Assert.Equal("Unit 101", produced.SubjectDisplay);
        var oldRecord = _store.Records.Single(r => r.SourceRecordId == ComplianceSourceIds.InsurancePolicy(first.Value));
        var newRecord = _store.Records.Single(r => r.SourceRecordId == ComplianceSourceIds.InsurancePolicy(second.Value));
        Assert.Equal(ComplianceRecordStatus.Superseded, oldRecord.Status);
        Assert.Equal(oldRecord.Id, newRecord.SupersedesId);
        Assert.All(_store.Policies, p => Assert.NotNull(p.DocumentNumber));
        Assert.Equal(AuthorityDocumentStatus.Superseded, _store.Policies.Single(p => p.Id == first.Value).Status);
    }

    [Fact]
    public async Task The_stamp_handler_ignores_other_modules_and_unowned_compliance_ids()
    {
        await RecordSfc("MB-SFC-001", new DateOnly(2026, 10, 1), new DateOnly(2027, 10, 1));
        var sourceId = Assert.Single(_store.Authorities).CurrentSfc!.SourceRecordId;

        await _stamp.Handle(new ComplianceRecordRegisteredIntegrationEvent(
            TestCompliance.TenantId, Guid.NewGuid(), "NL-VM-03-2026-0001", "R9", "fleet", sourceId), CancellationToken.None);
        await _stamp.Handle(new ComplianceRecordRegisteredIntegrationEvent(
            TestCompliance.TenantId, Guid.NewGuid(), "NL-SP-R21-2026-0001", "R21", "compliance", "manual:plan-v1"), CancellationToken.None);

        Assert.Null(_store.Authorities[0].CurrentSfc!.DocumentNumber);
    }

    /// <summary>Routes the intake's RegisterRecordCommand to the real handler (the only command it sends).</summary>
    private sealed class RegisterSender(RegisterRecordCommandHandler register) : ISender
    {
        public Task<Result> Send(ICommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public async Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) =>
            (Result<TResponse>)(object)await register.Handle((RegisterRecordCommand)(object)command, cancellationToken);

        public Task<Result<TResponse>> Query<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
