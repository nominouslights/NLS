using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Integration;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// In-memory fake of the user replica, mirroring
/// <c>Infrastructure/Persistence/UserLookupRepository.cs</c> — including its tenant scoping,
/// which is the part a fake is most tempting to skip. Both members carry the tenant
/// explicitly (the real ones bypass the EF filter and predicate on it), so keying this fake on
/// UserId alone would let the same user id in two tenants collapse to one row here while
/// staying two rows in production.
/// </summary>
internal sealed class InMemoryUserLookupRepository : IUserLookupRepository
{
    public List<UserLookup> Users { get; } = [];

    public Task<IReadOnlyList<UserLookup>> ListActiveByRolesAsync(
        Guid tenantId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserLookup>>(Users
            .Where(u => u.TenantId == tenantId && u.IsActive && roles.Contains(u.Role, StringComparer.Ordinal))
            .OrderBy(u => u.Email, StringComparer.Ordinal)
            .ToList());

    public Task UpsertAsync(UserLookup user, CancellationToken cancellationToken = default)
    {
        var existing = Users.FirstOrDefault(u => u.UserId == user.UserId && u.TenantId == user.TenantId);
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
            existing.IsActive = user.IsActive;
            existing.UpdatedAtUtc = user.UpdatedAtUtc;
        }

        return Task.CompletedTask;
    }

    /// <summary>Seeds one replica row; returns it so a test can flip <see cref="UserLookup.IsActive"/>.</summary>
    public UserLookup Seed(string role, string email, Guid? tenantId = null, string? fullName = null, bool isActive = true)
    {
        var user = new UserLookup
        {
            UserId = Guid.NewGuid(),
            TenantId = tenantId ?? TestNotifications.TenantId,
            Email = email,
            FullName = fullName,
            Role = role,
            IsActive = isActive,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        Users.Add(user);
        return user;
    }
}
