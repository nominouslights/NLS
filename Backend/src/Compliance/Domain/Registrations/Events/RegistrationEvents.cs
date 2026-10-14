using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Registrations.Events;

/// <summary>A registration term was recorded (first entry or renewal) — mapped to an R20 record-produced event.</summary>
public sealed record VehicleRegistrationRecordedDomainEvent(
    Guid RegistrationId,
    Guid TenantId,
    Guid VehicleId,
    string VehicleUnitNumber,
    string Plate,
    string VehicleClass,
    DateOnly EffectiveOn,
    DateOnly ExpiresOn,
    string? SupersedesSourceRecordId,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record VehicleRegistrationSupersededDomainEvent(Guid RegistrationId, Guid SupersededById, DateOnly SupersededOn, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record VehicleRegistrationAttachmentSetDomainEvent(Guid RegistrationId, string Slot, string StorageKey, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record VehicleRegistrationRecordStampedDomainEvent(Guid RegistrationId, Guid RecordId, string DocumentNumber) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
