using NorthernLink.Identity.Domain.Users.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Identity.Domain.Users;

/// <summary>
/// A platform user who can authenticate — the aggregate root behind the interim bespoke
/// JWT login flow. Today every user is Role "Admin" (Internal tenant); richer roles land
/// once the client apps beyond the Dispatch Console need their own accounts.
/// </summary>
public sealed class User : AggregateRoot, ITenantScoped
{
    private User()
    {
        // EF Core materialization only.
        Email = null!;
        PasswordHash = null!;
        Role = null!;
    }

    public Guid TenantId { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public string Role { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<User> Create(Guid tenantId, string email, string passwordHash, string role)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@'))
        {
            return Result.Failure<User>(UserErrors.InvalidEmail);
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result.Failure<User>(UserErrors.InvalidPasswordHash);
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            return Result.Failure<User>(UserErrors.InvalidRole);
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            TenantId = tenantId,
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            Role = role.Trim(),
            CreatedAtUtc = now,
        };

        user.Raise(new UserCreatedDomainEvent(user.Id, tenantId, user.Email, user.Role));
        return Result.Success(user);
    }
}
