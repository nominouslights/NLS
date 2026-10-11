using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Records.Events;

/// <summary>A record was registered and numbered. The mapper turns this into the public registered event.</summary>
public sealed record ComplianceRecordRegisteredDomainEvent(
    Guid RecordId,
    Guid TenantId,
    ComplianceRecordType RecordType,
    string DocumentNumber,
    string SourceModule,
    string SourceRecordId,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record SupportingDocumentLinkedDomainEvent(Guid RecordId, string Kind, string Label, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record AnchorDateSetDomainEvent(Guid RecordId, AnchorKind Anchor, DateOnly Date, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record RetentionRecomputedDomainEvent(Guid RecordId, DateOnly? RetainUntil, bool RetentionPending) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record RecordHoldPlacedDomainEvent(Guid RecordId, Guid HoldId, string Reason, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record RecordHoldReleasedDomainEvent(Guid RecordId, Guid HoldId, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record RecordSupersededDomainEvent(Guid RecordId, Guid SupersededById, DateOnly SupersededOn, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record RecordQueuedForDisposalReviewDomainEvent(Guid RecordId, DateOnly RetainUntil, DateOnly AsOf) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record RecordDisposedDomainEvent(Guid RecordId, string DocumentNumber, string Method, string? Note, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
