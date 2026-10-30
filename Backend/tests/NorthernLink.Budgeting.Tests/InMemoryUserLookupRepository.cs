using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Integration;

namespace NorthernLink.Budgeting.Tests;

/// <summary>
/// In-memory fake of the user replica, mirroring
/// <c>Infrastructure/Persistence/LookupRepositories.cs</c> exactly — including its tenant
/// scoping, which is the part a fake is most tempting to skip:
/// <list type="bullet">
/// <item><description><see cref="GetAsync"/> / <see cref="ListAsync"/> are the request path,
/// scoped by BudgetingDbContext's query filter (<c>u.TenantId == TenantId</c>) — modelled here
/// by <see cref="QueryFilterTenantId"/>.</description></item>
/// <item><description><see cref="UpsertAsync"/> is the integration-handler path, which runs
/// <c>IgnoreQueryFilters()</c> and therefore carries its own
/// <c>(UserId, TenantId)</c> predicate. Keying this fake on UserId alone would let the same
/// user id in two tenants collapse to one row here while staying two rows in production —
/// the divergence <c>UserChangedIntegrationEventHandlerTests</c> now pins.</description></item>
/// </list>
/// </summary>
internal sealed class InMemoryUserLookupRepository : IUserLookupRepository
{
    /// <summary>
    /// The tenant the read paths are scoped to — stands in for the DbContext's captured
    /// TenantId that drives the EF query filter.
    /// </summary>
    public Guid QueryFilterTenantId { get; init; } = TestBudgeting.TenantId;

    public List<UserLookup> Users { get; } = [];

    public Task<UserLookup?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(
            u => u.UserId == userId && u.TenantId == QueryFilterTenantId));

    public Task<IReadOnlyList<UserLookup>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserLookup>>(Users
            .Where(u => u.TenantId == QueryFilterTenantId)
            .OrderBy(u => u.Email, StringComparer.Ordinal)
            .ToList());

    public Task UpsertAsync(UserLookup user, CancellationToken cancellationToken = default)
    {
        var existing = Users.FirstOrDefault(
            u => u.UserId == user.UserId && u.TenantId == user.TenantId);
        if (existing is null)
        {
            Users.Add(user);
        }
        else
        {
            // Mirrors the real repository: every column the event carries is reassigned, nulls
            // included, so a cleared name clears here too.
            existing.Email = user.Email;
            existing.FullName = user.FullName;
            existing.Role = user.Role;
            existing.UpdatedAtUtc = user.UpdatedAtUtc;
        }

        return Task.CompletedTask;
    }
}
