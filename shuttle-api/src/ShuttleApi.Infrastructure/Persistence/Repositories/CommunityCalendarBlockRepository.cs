using Microsoft.EntityFrameworkCore;
using ShuttleApi.Domain.CommunityCalendar;

namespace ShuttleApi.Infrastructure.Persistence.Repositories;

internal sealed class CommunityCalendarBlockRepository(AppDbContext dbContext)
    : ICommunityCalendarBlockRepository
{
    public async Task<IReadOnlyList<CommunityCalendarBlock>> GetBlocksInRangeAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        await dbContext.CommunityCalendarBlocks
            .Where(b => b.BlockedDate >= from && b.BlockedDate <= to)
            .ToListAsync(cancellationToken);

    public async Task<CommunityCalendarBlock?> GetByDateAsync(
        DateOnly date, CancellationToken cancellationToken = default) =>
        await dbContext.CommunityCalendarBlocks
            .FirstOrDefaultAsync(b => b.BlockedDate == date, cancellationToken);

    public async Task AddAsync(
        CommunityCalendarBlock block, CancellationToken cancellationToken = default)
    {
        await dbContext.CommunityCalendarBlocks.AddAsync(block, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(
        CommunityCalendarBlock block, CancellationToken cancellationToken = default)
    {
        dbContext.CommunityCalendarBlocks.Remove(block);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
