namespace ShuttleApi.Application.Common.Interfaces;

/// <summary>
/// Query-side access to the audit event log. Domain event persistence is handled
/// atomically inside AppDbContext.SaveChangesAsync via the EF change tracker.
/// </summary>
public interface IAuditEventRepository
{
    Task<IReadOnlyList<AuditEventDto>> GetByAggregateAsync(string aggregateType, string aggregateId, CancellationToken cancellationToken = default);
}

public sealed record AuditEventDto(Guid Id, string EventType, string AggregateType, string AggregateId, string Payload, DateTime OccurredOn);
