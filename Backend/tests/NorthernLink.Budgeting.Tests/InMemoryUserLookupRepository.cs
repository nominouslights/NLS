using NorthernLink.Budgeting.Application.Abstractions;
using NorthernLink.Budgeting.Application.Integration;

namespace NorthernLink.Budgeting.Tests;

/// <summary>In-memory fake of the user replica for handler tests. Upsert is keyed on UserId.</summary>
internal sealed class InMemoryUserLookupRepository : IUserLookupRepository
{
    public List<UserLookup> Users { get; } = [];

    public Task<UserLookup?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.UserId == userId));

    public Task<IReadOnlyList<UserLookup>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserLookup>>(Users.OrderBy(u => u.Email, StringComparer.Ordinal).ToList());

    public Task UpsertAsync(UserLookup user, CancellationToken cancellationToken = default)
    {
        var existing = Users.FirstOrDefault(u => u.UserId == user.UserId);
        if (existing is null)
        {
            Users.Add(user);
        }
        else
        {
            existing.Email = user.Email;
            existing.Role = user.Role;
            existing.UpdatedAtUtc = user.UpdatedAtUtc;
        }

        return Task.CompletedTask;
    }
}
