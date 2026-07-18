using NorthernLink.Identity.Domain.Users;

namespace NorthernLink.Identity.Application.Abstractions;

/// <summary>
/// Write-side persistence for the User aggregate. <see cref="GetByEmailAsync"/> and
/// <see cref="GetByIdAsync"/> back the anonymous login/refresh flows, which run before any
/// tenant is known from an access token — implementations must bypass the normal
/// tenant-scoped query filter for those two lookups (see
/// <c>Infrastructure/Persistence/UserRepository.cs</c> for how RLS stays enforced anyway).
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(User user);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
