using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Domain.Users;

namespace NorthernLink.Identity.Tests;

/// <summary>In-memory fake of the bootstrap-token repository for handler tests.</summary>
internal sealed class InMemoryAdminBootstrapTokenRepository : IAdminBootstrapTokenRepository
{
    public List<AdminBootstrapToken> Tokens { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    public Task<AdminBootstrapToken?> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Tokens
            .Where(t => t.TenantId == tenantId && !t.IsConsumed)
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefault());

    public void Add(AdminBootstrapToken token) => Tokens.Add(token);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
