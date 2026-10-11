using NorthernLink.Compliance.Application.Records;
using NorthernLink.Compliance.Application.Records.Dispose;
using NorthernLink.Compliance.Application.Records.Holds;
using NorthernLink.Compliance.Application.Records.LinkSupportingDocument;
using NorthernLink.Compliance.Application.Records.QueueForDisposalReview;
using NorthernLink.Compliance.Application.Records.Register;
using NorthernLink.Compliance.Application.Records.SetAnchorDate;
using NorthernLink.Compliance.Application.Records.Supersede;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>The record lifecycle through the real handlers over the in-memory unit of work.</summary>
public class RecordHandlerTests
{
    private readonly InMemoryComplianceStore _store = new();
    private readonly RegisterRecordCommandHandler _register;
    private readonly SupersedeRecordCommandHandler _supersede;
    private readonly LinkSupportingDocumentCommandHandler _link;
    private readonly SetAnchorDateCommandHandler _setAnchor;
    private readonly PlaceHoldCommandHandler _placeHold;
    private readonly ReleaseHoldCommandHandler _releaseHold;
    private readonly DisposeRecordsCommandHandler _dispose;
    private readonly QueueForDisposalReviewCommandHandler _queue;

    public RecordHandlerTests()
    {
        var registrar = new RecordRegistrar(_store, _store, _store);
        _register = new RegisterRecordCommandHandler(registrar);
        _supersede = new SupersedeRecordCommandHandler(registrar);
        _link = new LinkSupportingDocumentCommandHandler(_store);
        _setAnchor = new SetAnchorDateCommandHandler(_store);
        _placeHold = new PlaceHoldCommandHandler(_store);
        _releaseHold = new ReleaseHoldCommandHandler(_store);
        _dispose = new DisposeRecordsCommandHandler(_store, _store);
        _queue = new QueueForDisposalReviewCommandHandler(_store);
    }

    private async Task<ComplianceRecord> RegisterAsync(
        ComplianceRecordType type = ComplianceRecordType.R9,
        RecordRegistration? registration = null)
    {
        var result = await _register.Handle(
            new RegisterRecordCommand(TestCompliance.TenantId, type, registration ?? TestCompliance.Registration(), TestCompliance.ActorId),
            CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return _store.Records.Single(r => r.Id == result.Value.RecordId);
    }

    private Task<Result<int>> QueueAsync(DateOnly asOf) =>
        _queue.Handle(new QueueForDisposalReviewCommand(TestCompliance.TenantId, asOf), CancellationToken.None);

    private Task<Result<DisposalResult>> DisposeAsync(params Guid[] ids) =>
        _dispose.Handle(new DisposeRecordsCommand(TestCompliance.TenantId, ids, "shredded", "end of retention", TestCompliance.ActorId), CancellationToken.None);

    // --- Register --------------------------------------------------------------------------------

    [Fact]
    public async Task Registering_computes_retention_from_the_type_rule_and_the_anchors()
    {
        var record = await RegisterAsync();

        Assert.Equal(ComplianceRecordStatus.Active, record.Status);
        Assert.Equal(new DateOnly(2027, 3, 30), record.RetainUntil); // 2026-09-30 month end + 6 months
        Assert.False(record.RetentionPending);
        Assert.Equal(1, record.DocumentVersion);
        Assert.Equal(TestCompliance.ActorId, record.RegisteredBy);
        Assert.Contains("NSC.13.16", record.RegulatoryBasisKeys);
        Assert.Equal("record-cover", record.PrintTemplateKey);
    }

    [Fact]
    public async Task Registering_the_same_source_twice_returns_the_first_record_unchanged()
    {
        var first = await _register.Handle(
            new RegisterRecordCommand(TestCompliance.TenantId, ComplianceRecordType.R9, TestCompliance.Registration(), TestCompliance.ActorId), CancellationToken.None);
        var second = await _register.Handle(
            new RegisterRecordCommand(TestCompliance.TenantId, ComplianceRecordType.R9, TestCompliance.Registration(title: "Different title"), TestCompliance.ActorId), CancellationToken.None);

        Assert.True(first.Value.Created);
        Assert.False(second.Value.Created);
        Assert.Equal(first.Value.RecordId, second.Value.RecordId);
        Assert.Equal(first.Value.DocumentNumber, second.Value.DocumentNumber);
        Assert.Single(_store.Records);
    }

    [Fact]
    public async Task Another_tenants_record_with_the_same_source_does_not_collide()
    {
        await RegisterAsync();
        _store.QueryFilterTenantId = Guid.NewGuid();

        var result = await _register.Handle(
            new RegisterRecordCommand(_store.QueryFilterTenantId, ComplianceRecordType.R9, TestCompliance.Registration(), null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Created);
        Assert.Equal(2, _store.Records.Count);
    }

    [Fact]
    public async Task Producer_extra_basis_keys_are_appended_to_the_types_own()
    {
        var record = await RegisterAsync(registration: TestCompliance.Registration(basisKeys: ["MR.95-2008.12.1", "NSC.13.16"]));

        Assert.Contains("MR.95-2008.12.1", record.RegulatoryBasisKeys);
        Assert.Single(record.RegulatoryBasisKeys, k => k == "NSC.13.16");
    }

    [Theory]
    [InlineData(null, "Compliance.Record.TitleRequired")]
    [InlineData("", "Compliance.Record.TitleRequired")]
    public async Task A_record_without_a_title_is_refused(string? title, string code)
    {
        var result = await _register.Handle(
            new RegisterRecordCommand(TestCompliance.TenantId, ComplianceRecordType.R9, TestCompliance.Registration(title: title), null), CancellationToken.None);

        Assert.Equal(code, result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Empty(_store.Records);
    }

    [Fact]
    public async Task A_record_without_a_source_key_is_refused()
    {
        var result = await _register.Handle(
            new RegisterRecordCommand(TestCompliance.TenantId, ComplianceRecordType.R9, TestCompliance.Registration(sourceRecordId: " "), null), CancellationToken.None);

        Assert.Equal("Compliance.Record.SourceRequired", result.Error.Code);
    }

    // --- Links and anchors -----------------------------------------------------------------------

    [Fact]
    public async Task Linking_a_supporting_document_appends_once()
    {
        var record = await RegisterAsync();
        var link = new SupportingLink("storage-key", "fleet/inspections/7f3c.pdf", "Signed report");

        var first = await _link.Handle(new LinkSupportingDocumentCommand(TestCompliance.TenantId, record.Id, link, TestCompliance.ActorId), CancellationToken.None);
        var again = await _link.Handle(new LinkSupportingDocumentCommand(TestCompliance.TenantId, record.Id, link, TestCompliance.ActorId), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(again.IsSuccess);
        Assert.Single(record.SupportingLinks);
    }

    [Fact]
    public async Task An_anchor_the_type_does_not_count_from_is_refused()
    {
        var record = await RegisterAsync(); // R9 counts from MonthEnd only

        var result = await _setAnchor.Handle(
            new SetAnchorDateCommand(TestCompliance.TenantId, record.Id, AnchorKind.ClaimClosed, new DateOnly(2027, 1, 1), null), CancellationToken.None);

        Assert.Equal("Compliance.Record.AnchorNotApplicable", result.Error.Code);
    }

    [Fact]
    public async Task Setting_the_later_anchor_settles_a_pending_record()
    {
        var incident = await RegisterAsync(ComplianceRecordType.R19, TestCompliance.Registration(
            sourceModule: "incidents", sourceRecordId: "inc:1",
            subject: new RecordSubject(RecordSubjectKind.Incident, Guid.NewGuid(), "Collision 2026-01-10"),
            anchors: TestCompliance.Anchors((AnchorKind.RecordDate, new DateOnly(2026, 1, 10)))));
        Assert.True(incident.RetentionPending);

        var result = await _setAnchor.Handle(
            new SetAnchorDateCommand(TestCompliance.TenantId, incident.Id, AnchorKind.ClaimClosed, new DateOnly(2028, 6, 15), TestCompliance.ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(incident.RetentionPending);
        Assert.Equal(new DateOnly(2028, 6, 15), incident.RetainUntil);
    }

    // --- Holds -----------------------------------------------------------------------------------

    [Fact]
    public async Task A_held_record_is_never_queued_for_disposal_until_released()
    {
        var record = await RegisterAsync();
        var hold = await _placeHold.Handle(new PlaceHoldCommand(TestCompliance.TenantId, record.Id, "MTI audit 2027", TestCompliance.ActorId), CancellationToken.None);
        Assert.True(hold.IsSuccess);
        Assert.True(record.IsHeld);

        var queuedWhileHeld = await QueueAsync(new DateOnly(2030, 1, 1));
        Assert.Equal(0, queuedWhileHeld.Value);
        Assert.Equal(ComplianceRecordStatus.Active, record.Status);

        var released = await _releaseHold.Handle(new ReleaseHoldCommand(TestCompliance.TenantId, record.Id, hold.Value, TestCompliance.ActorId), CancellationToken.None);
        Assert.True(released.IsSuccess);

        var queuedAfter = await QueueAsync(new DateOnly(2030, 1, 1));
        Assert.Equal(1, queuedAfter.Value);
        Assert.Equal(ComplianceRecordStatus.ReviewForDisposal, record.Status);
    }

    [Fact]
    public async Task A_hold_needs_a_reason_and_an_unknown_hold_cannot_be_released()
    {
        var record = await RegisterAsync();

        var noReason = await _placeHold.Handle(new PlaceHoldCommand(TestCompliance.TenantId, record.Id, "  ", null), CancellationToken.None);
        var unknown = await _releaseHold.Handle(new ReleaseHoldCommand(TestCompliance.TenantId, record.Id, Guid.NewGuid(), null), CancellationToken.None);

        Assert.Equal("Compliance.Record.HoldReasonRequired", noReason.Error.Code);
        Assert.Equal("Compliance.Record.HoldNotFound", unknown.Error.Code);
    }

    [Fact]
    public async Task A_hold_placed_on_a_record_in_review_pulls_it_back_to_active()
    {
        var record = await RegisterAsync();
        await QueueAsync(new DateOnly(2030, 1, 1));
        Assert.Equal(ComplianceRecordStatus.ReviewForDisposal, record.Status);

        await _placeHold.Handle(new PlaceHoldCommand(TestCompliance.TenantId, record.Id, "Claim opened", null), CancellationToken.None);

        Assert.Equal(ComplianceRecordStatus.Active, record.Status);
    }

    // --- Queue and dispose -----------------------------------------------------------------------

    [Fact]
    public async Task The_sweep_moves_only_records_whose_retention_has_passed()
    {
        var due = await RegisterAsync(); // retain until 2027-03-30
        var notYet = await RegisterAsync(ComplianceRecordType.R5, TestCompliance.Registration(
            sourceModule: "drivers", sourceRecordId: "nnc:1",
            anchors: TestCompliance.Anchors((AnchorKind.RecordDate, new DateOnly(2026, 9, 1))))); // 2028-09-01
        var pending = await RegisterAsync(ComplianceRecordType.R15, TestCompliance.Registration(
            sourceModule: "drivers", sourceRecordId: "licence:1",
            anchors: TestCompliance.Anchors((AnchorKind.RecordDate, new DateOnly(2020, 1, 1))))); // EmploymentEnd unknown

        var queued = await QueueAsync(new DateOnly(2027, 4, 1));

        Assert.Equal(1, queued.Value);
        Assert.Equal(ComplianceRecordStatus.ReviewForDisposal, due.Status);
        Assert.Equal(ComplianceRecordStatus.Active, notYet.Status);
        Assert.Equal(ComplianceRecordStatus.Active, pending.Status);
        Assert.True(pending.RetentionPending);
    }

    [Fact]
    public async Task A_record_on_its_retain_until_day_is_due()
    {
        var record = await RegisterAsync();

        Assert.Equal(1, (await QueueAsync(new DateOnly(2027, 3, 30))).Value);
        Assert.Equal(ComplianceRecordStatus.ReviewForDisposal, record.Status);
    }

    [Fact]
    public async Task Disposal_accounts_for_every_id_and_logs_each_destruction()
    {
        var reviewed = await RegisterAsync();
        var active = await RegisterAsync(ComplianceRecordType.R9, TestCompliance.Registration(sourceRecordId: "inspection:b"));
        var permanent = await RegisterAsync(ComplianceRecordType.R21, TestCompliance.Registration(
            sourceModule: "compliance", sourceRecordId: "plan:v1.7",
            subject: new RecordSubject(RecordSubjectKind.Carrier, null, "Northern Link Shuttle & Cargo"),
            anchors: TestCompliance.Anchors((AnchorKind.RecordDate, new DateOnly(2020, 1, 1)))));
        await _queue.Handle(new QueueForDisposalReviewCommand(TestCompliance.TenantId, new DateOnly(2027, 4, 1)), CancellationToken.None);
        // `active` is also due by that date — pull it back to Active with a hold so it reads as "held".
        await _placeHold.Handle(new PlaceHoldCommand(TestCompliance.TenantId, active.Id, "open claim", null), CancellationToken.None);
        var missing = Guid.NewGuid();

        var result = await DisposeAsync(reviewed.Id, active.Id, permanent.Id, missing);

        Assert.True(result.IsSuccess);
        Assert.Equal([reviewed.Id], result.Value.Disposed);
        Assert.Equal(3, result.Value.Refused.Count);
        Assert.Equal("Compliance.Record.Held", result.Value.Refused.Single(r => r.RecordId == active.Id).Code);
        Assert.Equal("Compliance.Record.Permanent", result.Value.Refused.Single(r => r.RecordId == permanent.Id).Code);
        Assert.Equal("Compliance.Record.NotFound", result.Value.Refused.Single(r => r.RecordId == missing).Code);
        Assert.All(result.Value.Refused, r => Assert.NotEmpty(r.Basis));

        Assert.Equal(ComplianceRecordStatus.Disposed, reviewed.Status);
        Assert.NotNull(reviewed.DisposedAtUtc);
        var log = Assert.Single(_store.DestructionLog);
        Assert.Equal(reviewed.Id, log.RecordId);
        Assert.Equal(reviewed.DocumentNumber, log.DocumentNumber);
        Assert.Equal("shredded", log.Method);
        Assert.Equal(TestCompliance.ActorId, log.DisposedBy);
    }

    [Fact]
    public async Task An_active_record_cannot_be_disposed_before_review()
    {
        var record = await RegisterAsync();

        var result = await DisposeAsync(record.Id);

        Assert.Empty(result.Value.Disposed);
        Assert.Equal("Compliance.Record.NotReviewable", Assert.Single(result.Value.Refused).Code);
        Assert.Empty(_store.DestructionLog);
    }

    [Fact]
    public async Task Disposal_needs_ids_and_a_method()
    {
        var noIds = await _dispose.Handle(new DisposeRecordsCommand(TestCompliance.TenantId, [], "shredded", null, null), CancellationToken.None);
        var noMethod = await _dispose.Handle(new DisposeRecordsCommand(TestCompliance.TenantId, [Guid.NewGuid()], " ", null, null), CancellationToken.None);

        Assert.Equal("Compliance.Record.DisposalIdsRequired", noIds.Error.Code);
        Assert.Equal("Compliance.Record.DisposalMethodRequired", noMethod.Error.Code);
    }

    // --- Supersede -------------------------------------------------------------------------------

    [Fact]
    public async Task Superseding_registers_version_two_and_starts_the_old_versions_clock()
    {
        var packet = await RegisterAsync(ComplianceRecordType.R6, TestCompliance.Registration(
            title: "ELD information packet v1", sourceModule: "compliance", sourceRecordId: "eld-packet:v1",
            subject: new RecordSubject(RecordSubjectKind.Carrier, null, "Northern Link Shuttle & Cargo"),
            anchors: TestCompliance.Anchors((AnchorKind.RecordDate, new DateOnly(2026, 1, 1)))));
        Assert.True(packet.RetentionPending);

        var result = await _supersede.Handle(new SupersedeRecordCommand(
            TestCompliance.TenantId, packet.Id,
            TestCompliance.Registration(title: "ELD information packet v2", sourceModule: "compliance", sourceRecordId: "eld-packet:v2",
                subject: new RecordSubject(RecordSubjectKind.Carrier, null, "Northern Link Shuttle & Cargo"),
                anchors: TestCompliance.Anchors((AnchorKind.RecordDate, DateOnly.FromDateTime(DateTime.UtcNow)))),
            TestCompliance.ActorId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        var successor = _store.Records.Single(r => r.Id == result.Value.RecordId);
        Assert.Equal(2, successor.DocumentVersion);
        Assert.Equal(packet.Id, successor.SupersedesId);
        Assert.NotEqual(packet.DocumentNumber, successor.DocumentNumber);

        Assert.Equal(ComplianceRecordStatus.Superseded, packet.Status);
        Assert.True(packet.AnchorDates.ContainsKey(AnchorKind.Superseded));
        Assert.False(packet.RetentionPending);
        Assert.Equal(packet.AnchorDates[AnchorKind.Superseded].AddMonths(6), packet.RetainUntil);
    }

    [Fact]
    public async Task Only_an_active_record_can_be_superseded()
    {
        var record = await RegisterAsync();
        await QueueAsync(new DateOnly(2030, 1, 1));
        await DisposeAsync(record.Id);

        var result = await _supersede.Handle(new SupersedeRecordCommand(
            TestCompliance.TenantId, record.Id, TestCompliance.Registration(sourceRecordId: "inspection:v2"), null), CancellationToken.None);

        Assert.Equal("Compliance.Record.NotActive", result.Error.Code);
    }

    [Fact]
    public async Task Superseding_a_missing_record_is_not_found()
    {
        var result = await _supersede.Handle(new SupersedeRecordCommand(
            TestCompliance.TenantId, Guid.NewGuid(), TestCompliance.Registration(), null), CancellationToken.None);

        Assert.Equal("Compliance.Record.NotFound", result.Error.Code);
    }
}
