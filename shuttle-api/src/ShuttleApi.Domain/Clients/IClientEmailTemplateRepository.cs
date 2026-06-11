namespace ShuttleApi.Domain.Clients;

public interface IClientEmailTemplateRepository
{
    Task<IReadOnlyList<ClientEmailTemplate>> GetByClientIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<ClientEmailTemplate?> GetByClientAndTypeAsync(Guid clientId, ClientEmailTemplateType type, CancellationToken cancellationToken = default);
    Task AddAsync(ClientEmailTemplate template, CancellationToken cancellationToken = default);
    Task UpdateAsync(ClientEmailTemplate template, CancellationToken cancellationToken = default);
}
