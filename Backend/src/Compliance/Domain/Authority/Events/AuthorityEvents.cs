using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Authority.Events;

public sealed record CarrierAuthorityOpenedDomainEvent(Guid AuthorityId, Guid TenantId, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A new SFC is in force. The mapper turns it into an R20 <c>ComplianceRecordProducedIntegrationEvent</c>;
/// <paramref name="SupersedesSourceRecordId"/> is the previous certificate's source id, or null for the first.
/// </summary>
public sealed record SafetyFitnessCertificateRecordedDomainEvent(
    Guid AuthorityId,
    Guid TenantId,
    SfcCertificate Certificate,
    string? SupersedesSourceRecordId,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record SfcIssueDateCorrectedDomainEvent(Guid AuthorityId, Guid CertificateId, DateOnly PreviousIssuedOn, DateOnly IssuedOn, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record NscNumberRecordedDomainEvent(Guid AuthorityId, string? PreviousNscNumber, string NscNumber, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record SafetyRatingSetDomainEvent(Guid AuthorityId, SafetyRating Rating, DateOnly AsOf, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record IntraProvincialDeclaredDomainEvent(Guid AuthorityId, DateOnly DeclaredOn, string SignerName, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record OperatingRoutesSetDomainEvent(Guid AuthorityId, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record SfcCertificateAttachedDomainEvent(Guid AuthorityId, Guid CertificateId, string StorageKey, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>The registry's record id and number were stamped onto a certificate this aggregate registered.</summary>
public sealed record AuthorityRecordStampedDomainEvent(Guid AuthorityId, string SourceRecordId, Guid RecordId, string DocumentNumber) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
