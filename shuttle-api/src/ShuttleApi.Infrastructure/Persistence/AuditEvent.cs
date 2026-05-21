namespace ShuttleApi.Infrastructure.Persistence;

public sealed class AuditEvent
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredOn { get; set; }
    public string? CorrelationId { get; set; }
}
