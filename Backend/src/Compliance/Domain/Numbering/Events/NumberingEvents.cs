using NorthernLink.Shared.Kernel;

namespace NorthernLink.Compliance.Domain.Numbering.Events;

public sealed record DocumentSeriesOpenedDomainEvent(Guid SeriesId, Guid TenantId, DocumentSeriesCode Series, int Year) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record DocumentNumberIssuedDomainEvent(
    Guid SeriesId, Guid TenantId, DocumentSeriesCode Series, int Year, int Sequence, string DocumentNumber) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ComplianceSettingsCreatedDomainEvent(Guid SettingsId, Guid TenantId, string DocumentPrefix, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ComplianceSettingsUpdatedDomainEvent(Guid SettingsId, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
