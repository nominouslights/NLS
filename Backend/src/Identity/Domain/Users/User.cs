using NorthernLink.Identity.Domain.Users.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Identity.Domain.Users;

/// <summary>
/// A platform user who can authenticate — the aggregate root behind the interim bespoke
/// JWT login flow. <see cref="Role"/> is one of <see cref="Roles.Internal"/> (Internal tenant);
/// Client, Vendor/Partner and Consumer roles land once those apps need their own accounts.
/// The role travels verbatim in the access token's "role" claim, so it is validated here at
/// creation rather than being trusted at authorization time.
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

        // Ordinal, case-sensitive — RequireRole compares the same way, so "owner" has to fail
        // here rather than authenticate fine and then 403 every request. Roles.LegacyAdmin is
        // deliberately not in Roles.Internal: no new user may be created as "Admin".
        var normalizedRole = role?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRole) || !Roles.IsKnown(normalizedRole))
        {
            return Result.Failure<User>(UserErrors.InvalidRole);
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            TenantId = tenantId,
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            Role = normalizedRole,
            CreatedAtUtc = now,
        };

        user.Raise(new UserCreatedDomainEvent(user.Id, tenantId, user.Email, user.Role));
        return Result.Success(user);
    }
}
