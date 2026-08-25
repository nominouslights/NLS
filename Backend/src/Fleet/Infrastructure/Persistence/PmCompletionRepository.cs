using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Maintenance;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="FleetDbContext"/> (tenant-filtered, append-only).</summary>
internal sealed class PmCompletionRepository(FleetDbContext context) : IPmCompletionRepository
{
    public Task<PmCompletion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.PmCompletions.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Add(PmCompletion completion) => context.PmCompletions.Add(completion);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
