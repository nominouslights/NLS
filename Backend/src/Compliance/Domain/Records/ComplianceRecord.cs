using NorthernLink.Compliance.Domain.Records.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Records;

/// <summary>
/// One entry in the carrier's record registry — the index card for a document some other module
/// (or a person) holds. Registering a record assigns its document number, fixes its retention
/// anchors and computes <see cref="RetainUntil"/>; the lifecycle then runs Active →
/// ReviewForDisposal (the nightly worker, once retention has passed and no hold applies) →
/// Disposed (a person, batch, logged), or Active → Superseded when a newer version is registered.
/// <para>
/// <b>What this is not.</b> It is not the document: trip inspection reports live in Fleet, duty
/// status in Drivers, manifests in Trips. Compliance only indexes them (via
/// <c>ComplianceRecordProducedIntegrationEvent</c>) and owns the retention decision.
/// </para>
/// <para>
/// <see cref="SourceModule"/> + <see cref="SourceRecordId"/> is unique per tenant — the
/// idempotency key for at-least-once registration. <see cref="DocumentVersion"/> is the document's
/// own version (v1, v2 after supersession); the inherited <see cref="AggregateRoot.Version"/> is
/// the persistence concurrency token and unrelated.
/// </para>
/// </summary>
public sealed class ComplianceRecord : AggregateRoot, ITenantScoped
{
    public const int TitleMaxLength = 200;
    public const int SourceModuleMaxLength = 64;
    public const int SourceRecordIdMaxLength = 128;
    public const int PrintTemplateKeyMaxLength = 64;

    private ComplianceRecord()
    {
        // EF Core materialization only.
        DocumentNumber = null!;
        Title = null!;
        Subject = null!;
        SourceModule = null!;
        SourceRecordId = null!;
        PrintTemplateKey = null!;
        AnchorDates = new Dictionary<AnchorKind, DateOnly>();
        Holds = [];
        SupportingLinks = [];
        RegulatoryBasisKeys = [];
    }

    public Guid TenantId { get; private set; }
    public ComplianceRecordType RecordType { get; private set; }

    /// <summary>The issued instance number, e.g. NL-VM-03-2026-0041. Immutable.</summary>
    public string DocumentNumber { get; private set; }

    public string Title { get; private set; }
    public RecordSubject Subject { get; private set; }
    public string SourceModule { get; private set; }
    public string SourceRecordId { get; private set; }

    /// <summary>The known anchor dates; a retention arm whose anchor is absent leaves the record pending.</summary>
    public IReadOnlyDictionary<AnchorKind, DateOnly> AnchorDates { get; private set; }

    public DateOnly? RetainUntil { get; private set; }

    /// <summary>True while an anchor the rule needs is unknown — the record stays Active regardless of age.</summary>
    public bool RetentionPending { get; private set; }

    public ComplianceRecordStatus Status { get; private set; }
    public IReadOnlyList<RecordHold> Holds { get; private set; }
    public IReadOnlyList<SupportingLink> SupportingLinks { get; private set; }
    public string PrintTemplateKey { get; private set; }

    /// <summary>The document's own version: 1 at first registration, +1 per supersession.</summary>
    public int DocumentVersion { get; private set; }

    public Guid? SupersedesId { get; private set; }
    public IReadOnlyList<string> RegulatoryBasisKeys { get; private set; }
    public Guid? RegisteredBy { get; private set; }
    public DateTimeOffset RegisteredAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DisposedAtUtc { get; private set; }

    public bool IsHeld => Holds.Count > 0;

    /// <summary>
    /// Registers a record: validates the registration, derives MonthEnd from RecordDate when
    /// needed, computes retention from the type's rule and stamps the issued number.
    /// </summary>
    public static Result<ComplianceRecord> Register(
        Guid tenantId,
        RecordTypeDefinition definition,
        string documentNumber,
        RecordRegistration registration,
        Guid? actorId,
        DateTimeOffset now)
    {
        var normalized = Normalize(definition, registration);
        if (normalized.IsFailure)
        {
            return Result.Failure<ComplianceRecord>(normalized.Error);
        }

        var record = Build(tenantId, definition, documentNumber, normalized.Value, documentVersion: 1, supersedesId: null, actorId, now);
        return Result.Success(record);
    }

    /// <summary>Attaches a pointer to a supporting document. Duplicate (kind, reference) pairs are a silent success.</summary>
    public Result LinkSupportingDocument(SupportingLink link, Guid? actorId)
    {
        if (Status != ComplianceRecordStatus.Active)
        {
            return Result.Failure(ComplianceRecordErrors.NotActive);
        }

        var trimmed = TrimLink(link);
        if (trimmed is null)
        {
            return Result.Failure(ComplianceRecordErrors.SupportingLinkInvalid);
        }

        if (SupportingLinks.Any(l => l.Kind == trimmed.Kind && l.Reference == trimmed.Reference))
        {
            return Result.Success();
        }

        SupportingLinks = [.. SupportingLinks, trimmed];
        Touch(actorId);
        Raise(new SupportingDocumentLinkedDomainEvent(Id, trimmed.Kind, trimmed.Label, actorId));
        return Result.Success();
    }

    /// <summary>
    /// Records an anchor date learned after registration (a claim closed, a vehicle sold, an
    /// employee leaving) and recomputes retention. Only anchors the type's rule counts from are
    /// accepted — setting ClaimClosed on a trip inspection report is a mistake, not data.
    /// </summary>
    public Result SetAnchorDate(RetentionRule rule, AnchorKind anchor, DateOnly date, Guid? actorId)
    {
        if (Status is ComplianceRecordStatus.Disposed)
        {
            return Result.Failure(ComplianceRecordErrors.NotActive);
        }

        if (!rule.Anchors.Contains(anchor))
        {
            return Result.Failure(ComplianceRecordErrors.AnchorNotApplicable);
        }

        var anchors = new Dictionary<AnchorKind, DateOnly>(AnchorDates) { [anchor] = date };
        AnchorDates = anchors;
        Touch(actorId);
        Raise(new AnchorDateSetDomainEvent(Id, anchor, date, actorId));
        RecomputeRetention(rule);

        // The review was decided on the old retain-until. A corrected anchor can push it into the
        // future, and Dispose trusts the review status — so the record leaves review and the next
        // sweep re-queues it only if it is still due.
        if (Status == ComplianceRecordStatus.ReviewForDisposal)
        {
            Status = StatusBeforeReview;
        }

        return Result.Success();
    }

    /// <summary>Re-runs the calculator; raises an event only when the outcome changed. Returns true when it did.</summary>
    public bool RecomputeRetention(RetentionRule rule)
    {
        var outcome = RetentionCalculator.Compute(rule, AnchorDates);
        var retainUntil = outcome.Permanent ? null : outcome.RetainUntil;
        var pending = outcome.Pending;

        if (retainUntil == RetainUntil && pending == RetentionPending)
        {
            return false;
        }

        RetainUntil = retainUntil;
        RetentionPending = pending;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new RetentionRecomputedDomainEvent(Id, RetainUntil, RetentionPending));
        return true;
    }

    public Result<Guid> PlaceHold(string? reason, Guid? placedBy, DateTimeOffset now)
    {
        if (Status is ComplianceRecordStatus.Disposed)
        {
            return Result.Failure<Guid>(ComplianceRecordErrors.NotActive);
        }

        var trimmedReason = reason?.Trim();
        if (string.IsNullOrEmpty(trimmedReason) || trimmedReason.Length > RecordHold.ReasonMaxLength)
        {
            return Result.Failure<Guid>(ComplianceRecordErrors.HoldReasonRequired);
        }

        var hold = new RecordHold(Guid.NewGuid(), trimmedReason, placedBy, now);
        Holds = [.. Holds, hold];

        // A hold pulls a record back out of review: it must not be disposable while held. A
        // superseded version goes back to Superseded, never to Active — it is not the current one.
        if (Status == ComplianceRecordStatus.ReviewForDisposal)
        {
            Status = StatusBeforeReview;
        }

        Touch(placedBy);
        Raise(new RecordHoldPlacedDomainEvent(Id, hold.HoldId, trimmedReason, placedBy));
        return Result.Success(hold.HoldId);
    }

    public Result ReleaseHold(Guid holdId, Guid? actorId)
    {
        if (Holds.All(h => h.HoldId != holdId))
        {
            return Result.Failure(ComplianceRecordErrors.HoldNotFound);
        }

        Holds = Holds.Where(h => h.HoldId != holdId).ToList();
        Touch(actorId);
        Raise(new RecordHoldReleasedDomainEvent(Id, holdId, actorId));
        return Result.Success();
    }

    /// <summary>
    /// Registers a new version of this document and retires this one: the new record carries
    /// <c>DocumentVersion + 1</c> and <c>SupersedesId = this</c>; this record becomes Superseded
    /// with its <see cref="AnchorKind.Superseded"/> anchor set to today, so a type whose retention
    /// counts from supersession (R6, R11, R20) starts its clock now.
    /// </summary>
    public Result<ComplianceRecord> Supersede(
        RecordTypeDefinition definition,
        string newDocumentNumber,
        RecordRegistration registration,
        Guid? actorId,
        DateTimeOffset now)
    {
        if (Status != ComplianceRecordStatus.Active)
        {
            return Result.Failure<ComplianceRecord>(ComplianceRecordErrors.NotActive);
        }

        var normalized = Normalize(definition, registration);
        if (normalized.IsFailure)
        {
            return Result.Failure<ComplianceRecord>(normalized.Error);
        }

        var successor = Build(TenantId, definition, newDocumentNumber, normalized.Value, DocumentVersion + 1, Id, actorId, now);

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        Status = ComplianceRecordStatus.Superseded;
        AnchorDates = new Dictionary<AnchorKind, DateOnly>(AnchorDates) { [AnchorKind.Superseded] = today };
        Touch(actorId);
        Raise(new RecordSupersededDomainEvent(Id, successor.Id, today, actorId));
        RecomputeRetention(definition.Rule);

        return Result.Success(successor);
    }

    /// <summary>
    /// The nightly worker's transition: an Active (or Superseded) record whose retention date has
    /// passed and that carries no hold moves to ReviewForDisposal. Pending and permanent records
    /// never move. Returns true when the status changed.
    /// </summary>
    public bool QueueForDisposalReview(DateOnly asOf)
    {
        if (Status is not (ComplianceRecordStatus.Active or ComplianceRecordStatus.Superseded))
        {
            return false;
        }

        if (RetentionPending || RetainUntil is not { } retainUntil || retainUntil > asOf || IsHeld)
        {
            return false;
        }

        Status = ComplianceRecordStatus.ReviewForDisposal;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new RecordQueuedForDisposalReviewDomainEvent(Id, retainUntil, asOf));
        return true;
    }

    /// <summary>Confirms destruction. Only a reviewed, unheld, non-permanent record may be disposed.</summary>
    public Result Dispose(RetentionRule rule, string? method, string? note, Guid? actorId, DateTimeOffset now)
    {
        if (rule.IsPermanent)
        {
            return Result.Failure(ComplianceRecordErrors.Permanent);
        }

        if (IsHeld)
        {
            return Result.Failure(ComplianceRecordErrors.Held);
        }

        if (Status != ComplianceRecordStatus.ReviewForDisposal)
        {
            return Result.Failure(ComplianceRecordErrors.NotReviewable);
        }

        var trimmedMethod = method?.Trim();
        if (string.IsNullOrEmpty(trimmedMethod) || trimmedMethod.Length > DestructionLogEntry.MethodMaxLength)
        {
            return Result.Failure(ComplianceRecordErrors.DisposalMethodRequired);
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote?.Length > DestructionLogEntry.NoteMaxLength)
        {
            trimmedNote = trimmedNote[..DestructionLogEntry.NoteMaxLength];
        }

        Status = ComplianceRecordStatus.Disposed;
        DisposedAtUtc = now;
        Touch(actorId);
        Raise(new RecordDisposedDomainEvent(Id, DocumentNumber, trimmedMethod, trimmedNote, actorId));
        return Result.Success();
    }

    private static ComplianceRecord Build(
        Guid tenantId,
        RecordTypeDefinition definition,
        string documentNumber,
        NormalizedRegistration details,
        int documentVersion,
        Guid? supersedesId,
        Guid? actorId,
        DateTimeOffset now)
    {
        var record = new ComplianceRecord
        {
            TenantId = tenantId,
            RecordType = definition.Type,
            DocumentNumber = documentNumber,
            Title = details.Title,
            Subject = details.Subject,
            SourceModule = details.SourceModule,
            SourceRecordId = details.SourceRecordId,
            AnchorDates = details.AnchorDates,
            Status = ComplianceRecordStatus.Active,
            Holds = [],
            SupportingLinks = details.SupportingLinks,
            PrintTemplateKey = details.PrintTemplateKey,
            DocumentVersion = documentVersion,
            SupersedesId = supersedesId,
            RegulatoryBasisKeys = details.RegulatoryBasisKeys,
            RegisteredBy = actorId,
            RegisteredAtUtc = now,
            UpdatedAtUtc = now,
        };

        var outcome = RetentionCalculator.Compute(definition.Rule, record.AnchorDates);
        record.RetainUntil = outcome.Permanent ? null : outcome.RetainUntil;
        record.RetentionPending = outcome.Pending;

        record.Raise(new ComplianceRecordRegisteredDomainEvent(
            record.Id, tenantId, definition.Type, documentNumber, details.SourceModule, details.SourceRecordId, actorId));
        return record;
    }

    /// <summary>
    /// Where a record in ReviewForDisposal returns to. Review does not store its origin, but only
    /// Active and Superseded records are ever queued, and a replaced document carries the
    /// <see cref="AnchorKind.Superseded"/> anchor (set by <see cref="Supersede"/>, or by its
    /// producer when it reports the replacement) — so the anchor says which one it was.
    /// </summary>
    private ComplianceRecordStatus StatusBeforeReview =>
        AnchorDates.ContainsKey(AnchorKind.Superseded) ? ComplianceRecordStatus.Superseded : ComplianceRecordStatus.Active;

    private void Touch(Guid? actorId)
    {
        _ = actorId; // actors ride the events; the row keeps only timestamps
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private sealed record NormalizedRegistration(
        string Title,
        RecordSubject Subject,
        string SourceModule,
        string SourceRecordId,
        IReadOnlyDictionary<AnchorKind, DateOnly> AnchorDates,
        IReadOnlyList<SupportingLink> SupportingLinks,
        IReadOnlyList<string> RegulatoryBasisKeys,
        string PrintTemplateKey);

    private static Result<NormalizedRegistration> Normalize(RecordTypeDefinition definition, RecordRegistration registration)
    {
        var title = registration.Title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            return Fail(ComplianceRecordErrors.TitleRequired);
        }

        if (title.Length > TitleMaxLength)
        {
            return Fail(ComplianceRecordErrors.TitleTooLong);
        }

        if (registration.Subject is null || string.IsNullOrWhiteSpace(registration.Subject.Display))
        {
            return Fail(ComplianceRecordErrors.SubjectRequired);
        }

        var subject = registration.Subject with { Display = registration.Subject.Display.Trim() };
        if (subject.Display.Length > RecordSubject.DisplayMaxLength)
        {
            return Fail(ComplianceRecordErrors.SubjectDisplayTooLong);
        }

        var sourceModule = registration.SourceModule?.Trim();
        var sourceRecordId = registration.SourceRecordId?.Trim();
        if (string.IsNullOrEmpty(sourceModule) || string.IsNullOrEmpty(sourceRecordId))
        {
            return Fail(ComplianceRecordErrors.SourceRequired);
        }

        if (sourceModule.Length > SourceModuleMaxLength || sourceRecordId.Length > SourceRecordIdMaxLength)
        {
            return Fail(ComplianceRecordErrors.SourceTooLong);
        }

        var links = new List<SupportingLink>();
        foreach (var link in registration.SupportingLinks ?? [])
        {
            var trimmed = TrimLink(link);
            if (trimmed is null)
            {
                return Fail(ComplianceRecordErrors.SupportingLinkInvalid);
            }

            links.Add(trimmed);
        }

        var templateKey = string.IsNullOrWhiteSpace(registration.PrintTemplateKey)
            ? definition.PrintTemplateKey
            : registration.PrintTemplateKey.Trim();
        if (templateKey.Length > PrintTemplateKeyMaxLength)
        {
            return Fail(ComplianceRecordErrors.PrintTemplateKeyTooLong);
        }

        var anchors = RetentionCalculator.WithDerivedAnchors(
            registration.AnchorDates ?? new Dictionary<AnchorKind, DateOnly>());

        // The type's own basis keys always apply; a producer's extra keys are appended, de-duplicated.
        var basisKeys = definition.RegulatoryBasisKeys
            .Concat((registration.RegulatoryBasisKeys ?? []).Select(k => k.Trim()).Where(k => k.Length > 0))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return Result.Success(new NormalizedRegistration(
            title, subject, sourceModule, sourceRecordId, anchors, links, basisKeys, templateKey));

        static Result<NormalizedRegistration> Fail(Error error) => Result.Failure<NormalizedRegistration>(error);
    }

    private static SupportingLink? TrimLink(SupportingLink link)
    {
        var kind = link.Kind?.Trim();
        var reference = link.Reference?.Trim();
        var label = link.Label?.Trim();
        if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(reference) || string.IsNullOrEmpty(label)
            || kind.Length > SupportingLink.KindMaxLength
            || reference.Length > SupportingLink.ReferenceMaxLength
            || label.Length > SupportingLink.LabelMaxLength)
        {
            return null;
        }

        return new SupportingLink(kind, reference, label);
    }
}
