using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Users;

namespace ShuttleApi.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Collect domain events from all modified aggregates before saving
        var aggregates = ChangeTracker.Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var auditEvents = aggregates
            .SelectMany(aggregate => aggregate.DomainEvents.Select(evt => new AuditEvent
            {
                Id = evt.EventId,
                EventType = evt.EventType,
                AggregateType = aggregate.GetType().Name,
                AggregateId = aggregate.GetId()?.ToString() ?? string.Empty,
                Payload = JsonSerializer.Serialize(evt, evt.GetType()),
                OccurredOn = evt.OccurredOn
            }))
            .ToList();

        if (auditEvents.Count > 0)
            AuditEvents.AddRange(auditEvents);

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var aggregate in aggregates)
            aggregate.ClearDomainEvents();

        return result;
    }
}
