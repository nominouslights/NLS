using Microsoft.EntityFrameworkCore;
using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Infrastructure.Persistence.Repositories;

public sealed class AuditEventRepository(AppDbContext dbContext) : IAuditEventRepository
{
    public async Task<IReadOnlyList<AuditEventDto>> GetByAggregateAsync(
        string aggregateType,
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        var events = await dbContext.AuditEvents
            .AsNoTracking()
            .Where(e => e.AggregateType == aggregateType && e.AggregateId == aggregateId)
            .OrderBy(e => e.OccurredOn)
            .Select(e => new AuditEventDto(e.Id, e.EventType, e.AggregateType, e.AggregateId, e.Payload, e.OccurredOn))
            .ToListAsync(cancellationToken);

        return events;
    }
}
