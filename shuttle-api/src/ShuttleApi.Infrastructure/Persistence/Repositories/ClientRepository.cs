using Microsoft.EntityFrameworkCore;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Infrastructure.Persistence.Repositories;

internal sealed class ClientRepository(AppDbContext dbContext) : IClientRepository
{
    public async Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Clients.AsNoTracking().OrderBy(c => c.BusinessName).ToListAsync(cancellationToken);

    public async Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Clients
            .Include(c => c.NotificationEmails)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task AddAsync(Client client, CancellationToken cancellationToken = default)
    {
        await dbContext.Clients.AddAsync(client, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Client client, CancellationToken cancellationToken = default)
    {
        if (dbContext.Entry(client).State == EntityState.Detached)
            dbContext.Clients.Update(client);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Client client, CancellationToken cancellationToken = default)
    {
        dbContext.Clients.Remove(client);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
