using Microsoft.EntityFrameworkCore;
using ShuttleApi.Domain.Passengers;

namespace ShuttleApi.Infrastructure.Persistence.Repositories;

internal sealed class PassengerProfileRepository(AppDbContext dbContext)
    : IPassengerProfileRepository
{
    public async Task<IReadOnlyList<PassengerProfile>> SearchAsync(
        Guid clientId, string query, CancellationToken cancellationToken = default)
    {
        var q = query.Trim().ToLowerInvariant();
        return await dbContext.PassengerProfiles
            .AsNoTracking()
            .Where(p => p.ClientId == clientId &&
                        (q == string.Empty || p.NormalizedName.Contains(q)))
            .OrderByDescending(p => p.LastBookedAt)
            .Take(10)
            .ToListAsync(cancellationToken);
    }

    public async Task<PassengerProfile?> FindByNormalizedNameAsync(
        Guid clientId, string normalizedName, CancellationToken cancellationToken = default) =>
        await dbContext.PassengerProfiles
            .FirstOrDefaultAsync(p => p.ClientId == clientId &&
                                      p.NormalizedName == normalizedName, cancellationToken);

    public async Task AddAsync(PassengerProfile profile, CancellationToken cancellationToken = default)
    {
        await dbContext.PassengerProfiles.AddAsync(profile, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PassengerProfile profile, CancellationToken cancellationToken = default)
    {
        dbContext.PassengerProfiles.Update(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task PurgeExpiredAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var expired = await dbContext.PassengerProfiles
            .Where(p => p.LastBookedAt < cutoffUtc)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0) return;

        dbContext.PassengerProfiles.RemoveRange(expired);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
