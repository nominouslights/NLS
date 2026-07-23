using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Domain.Users;

namespace NorthernLink.Identity.Tests;

/// <summary>In-memory fake of the write-side User repository for handler tests.</summary>
internal sealed class InMemoryUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public int SaveChangesCallCount { get; private set; }

    /// <summary>
    /// Forces the next <see cref="TryAddNewUserAsync"/> to report a unique-index violation
    /// even though no matching user is visible — simulates the pre-check→insert race the
    /// real repository closes at the database.
    /// </summary>
    public bool FailNextTryAddNewUser { get; set; }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return Task.FromResult(Users.FirstOrDefault(u => u.Email == normalizedEmail));
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Count > 0);

    public Task<bool> TryAddFirstUserAsync(User user, CancellationToken cancellationToken = default)
    {
        if (Users.Count > 0)
        {
            return Task.FromResult(false);
        }

        Users.Add(user);
        SaveChangesCallCount++;
        return Task.FromResult(true);
    }

    public Task<bool> TryAddNewUserAsync(User user, CancellationToken cancellationToken = default)
    {
        if (FailNextTryAddNewUser || Users.Any(u => u.Email == user.Email))
        {
            FailNextTryAddNewUser = false;
            return Task.FromResult(false);
        }

        Users.Add(user);
        SaveChangesCallCount++;
        return Task.FromResult(true);
    }

    public void Add(User user) => Users.Add(user);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
