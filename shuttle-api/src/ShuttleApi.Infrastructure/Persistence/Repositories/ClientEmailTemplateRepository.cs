using Microsoft.EntityFrameworkCore;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Infrastructure.Persistence.Repositories;

internal sealed class ClientEmailTemplateRepository(AppDbContext dbContext) : IClientEmailTemplateRepository
{
    public async Task<IReadOnlyList<ClientEmailTemplate>> GetByClientIdAsync(
        Guid clientId, CancellationToken cancellationToken = default) =>
        await dbContext.ClientEmailTemplates
            .AsNoTracking()
            .Where(t => t.ClientId == clientId)
            .OrderBy(t => t.Type)
            .ToListAsync(cancellationToken);

    public async Task<ClientEmailTemplate?> GetByClientAndTypeAsync(
        Guid clientId, ClientEmailTemplateType type, CancellationToken cancellationToken = default) =>
        await dbContext.ClientEmailTemplates
            .FirstOrDefaultAsync(t => t.ClientId == clientId && t.Type == type, cancellationToken);

    public async Task AddAsync(ClientEmailTemplate template, CancellationToken cancellationToken = default)
    {
        await dbContext.ClientEmailTemplates.AddAsync(template, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ClientEmailTemplate template, CancellationToken cancellationToken = default)
    {
        dbContext.ClientEmailTemplates.Update(template);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
