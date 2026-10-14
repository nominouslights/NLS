using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Insurance.Events;

/// <summary>
/// A policy was recorded (first entry or renewal). The mapper turns it into an R20
/// record-produced integration event; <paramref name="SupersedesSourceRecordId"/> names the
/// renewed predecessor.
/// </summary>
public sealed record InsurancePolicyRecordedDomainEvent(
    Guid PolicyId,
    Guid TenantId,
    Guid VehicleId,
    string VehicleUnitNumber,
    string Insurer,
    string PolicyNumber,
    decimal LiabilityLimitCad,
    bool BelowMinimum,
    DateOnly EffectiveOn,
    DateOnly ExpiresOn,
    string? SupersedesSourceRecordId,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record InsurancePolicySupersededDomainEvent(Guid PolicyId, Guid SupersededById, DateOnly SupersededOn, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record InsurancePolicyAttachmentSetDomainEvent(Guid PolicyId, string Slot, string StorageKey, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record InsurancePolicyRecordStampedDomainEvent(Guid PolicyId, Guid RecordId, string DocumentNumber) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
