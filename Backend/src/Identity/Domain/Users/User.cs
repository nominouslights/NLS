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

    /// <summary>
    /// The user's own display name, or null when they have not set one — which is every account
    /// created before profiles existed. Null rather than an empty string on purpose: "no name"
    /// is a real state the whole platform has to render a fallback for (see Budgeting's
    /// <c>user_lookup</c>, which falls back to the email), and null is what lets a user clear a
    /// name they had previously set.
    /// </summary>
    public string? FullName { get; private set; }

    /// <summary>Free text — the user's own words for what they do. Never an authorization input;
    /// <see cref="Role"/> is the only thing that grants anything.</summary>
    public string? JobTitle { get; private set; }

    /// <summary>Longest a <see cref="FullName"/> or <see cref="JobTitle"/> may be, after
    /// trimming. Mirrored by the column lengths, by Budgeting's replica column, and client-side
    /// in <c>Budgeting/lib/api/identity.ts</c>.</summary>
    public const int ProfileFieldMaxLength = 128;

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

    /// <summary>
    /// Sets the user's own name and job title — the self-service profile write. Both are
    /// optional: whitespace-only (or null) clears the field rather than storing blank, so
    /// "remove my job title" round-trips honestly all the way to Budgeting's replica.
    /// <para>
    /// An update that changes nothing raises no event and assigns nothing. That is deliberate,
    /// not an optimization: every save writes an aggregate snapshot, an event-journal row and an
    /// outbox row, and bumps the concurrency version, so a planner pressing SAVE twice must not
    /// produce two of each. It is also why this aggregate has no <c>UpdatedAtUtc</c> — stamping
    /// one would mark the entity Modified on the no-op path, and
    /// <c>ModuleDbContext.AppendAuditEntries</c> throws on a modified aggregate that raised no
    /// domain event.
    /// </para>
    /// </summary>
    public Result UpdateProfile(string? fullName, string? jobTitle)
    {
        var normalizedName = Normalize(fullName);
        var normalizedTitle = Normalize(jobTitle);

        // Both are validated before either is assigned, so a too-long job title cannot leave a
        // good name half-applied.
        if (normalizedName is { Length: > ProfileFieldMaxLength })
        {
            return Result.Failure(UserErrors.FullNameTooLong);
        }

        if (normalizedTitle is { Length: > ProfileFieldMaxLength })
        {
            return Result.Failure(UserErrors.JobTitleTooLong);
        }

        if (normalizedName == FullName && normalizedTitle == JobTitle)
        {
            return Result.Success();
        }

        FullName = normalizedName;
        JobTitle = normalizedTitle;

        Raise(new UserProfileUpdatedDomainEvent(Id, TenantId, FullName, JobTitle));
        return Result.Success();
    }

    /// <summary>Trims, then treats blank as absent. No content rules beyond length — a name is
    /// whatever the person says it is.</summary>
    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
