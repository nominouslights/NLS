using NorthernLink.Drivers.Domain.Drivers.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Drivers;

/// <summary>
/// A driver on the roster — Northern Link's own or a partner's (<see cref="Source"/>
/// carries "Northern Link" or the partner name). Trips reference drivers by id via the
/// <c>drivers.driver-changed</c> integration event; credentials and client clearances
/// are separate aggregates keyed on <see cref="AggregateRoot.Id"/>. Credential expiry
/// chips (valid / expiring / expired) are derived by the frontend from the dates, so
/// nothing like that is stored here. HOS stays display-only mock — out of scope.
/// </summary>
public sealed class Driver : AggregateRoot, ITenantScoped
{
    private Driver()
    {
        // EF Core materialization only.
        Name = null!;
        LicenceClass = null!;
        Source = null!;
    }

    public Guid TenantId { get; private set; }

    /// <summary>
    /// The Identity user (the access token's <c>sub</c>) this roster row belongs to, or null when
    /// nobody can log in as this driver. This is what makes <c>GET /api/drivers/me</c> possible
    /// and what the caller-owns-this-row check resolves against.
    /// <para>
    /// <b>The link lives here, on Drivers, deliberately.</b> The mirror image — a
    /// <c>User.DriverId</c> plus a <c>driver_id</c> JWT claim — would have Identity storing a fact
    /// it does not own and cannot validate, and the claim would go stale for up to an access
    /// token's 15-minute life after a link changes. Matching on email was rejected too: a Driver
    /// has no email field, and matching a mutable string where a Guid is exact is a heuristic.
    /// </para>
    /// <para>
    /// A bare <c>Guid?</c> is not a type reference, so this creates no dependency on the Identity
    /// library and the architecture tests stay green — the same precedent as <c>Trip.DriverId</c>
    /// and every <c>ITenantScoped.TenantId</c>.
    /// </para>
    /// </summary>
    public Guid? UserId { get; private set; }

    public string Name { get; private set; }
    public string? Phone { get; private set; }
    public string LicenceClass { get; private set; }
    public DateOnly? LicenceExpiry { get; private set; }
    public string Source { get; private set; }
    public bool HasWorkPermit { get; private set; }
    public DriverStatus Status { get; private set; }
    public DateTimeOffset RegisteredAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// The lifecycle transition matrix. Diagonal (same status) is not a transition.
    /// Deactivated is off-roster: the only way back is reinstatement to Active.
    /// </summary>
    public static bool CanTransition(DriverStatus from, DriverStatus to) =>
        (from, to) switch
        {
            (DriverStatus.Active, DriverStatus.Inactive or DriverStatus.Deactivated) => true,
            (DriverStatus.Inactive, DriverStatus.Active or DriverStatus.Deactivated) => true,
            (DriverStatus.Deactivated, DriverStatus.Active) => true,
            _ => false,
        };

    public static Result<Driver> Register(
        Guid tenantId,
        string name,
        string? phone,
        string licenceClass,
        DateOnly? licenceExpiry,
        string source,
        bool hasWorkPermit)
    {
        var validation = ValidateDetails(name, licenceClass, source);
        if (validation.IsFailure)
        {
            return Result.Failure<Driver>(validation.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var driver = new Driver
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            LicenceClass = licenceClass.Trim(),
            LicenceExpiry = licenceExpiry,
            Source = source.Trim(),
            HasWorkPermit = hasWorkPermit,
            Status = DriverStatus.Active,
            RegisteredAtUtc = now,
            UpdatedAtUtc = now,
        };

        driver.Raise(new DriverRegisteredDomainEvent(driver.Id, tenantId));
        return Result.Success(driver);
    }

    /// <summary>Updates registration details. Status changes through <see cref="ChangeStatus"/>.</summary>
    public Result Update(
        string name,
        string? phone,
        string licenceClass,
        DateOnly? licenceExpiry,
        string source,
        bool hasWorkPermit)
    {
        var validation = ValidateDetails(name, licenceClass, source);
        if (validation.IsFailure)
        {
            return validation;
        }

        Name = name.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        LicenceClass = licenceClass.Trim();
        LicenceExpiry = licenceExpiry;
        Source = source.Trim();
        HasWorkPermit = hasWorkPermit;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new DriverUpdatedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>Moves the driver through the transition matrix.</summary>
    public Result ChangeStatus(DriverStatus newStatus)
    {
        if (!CanTransition(Status, newStatus))
        {
            return Result.Failure(DriverErrors.InvalidStatusTransition(Status, newStatus));
        }

        var previous = Status;
        Status = newStatus;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new DriverStatusChangedDomainEvent(Id, previous, newStatus));
        return Result.Success();
    }

    /// <summary>
    /// Links this driver to an Identity user, so that user can sign into the Driver Field App and
    /// see this roster row as "me".
    /// <para>
    /// Its own transition, like <see cref="ChangeStatus"/> — deliberately NOT settable through
    /// <see cref="Register"/> or <see cref="Update"/>. Who may sign in as a driver is an access
    /// decision, and folding it into the roster-details form would let an ordinary "fix the phone
    /// number" edit silently re-point a login, with no distinct event in the journal to show it.
    /// </para>
    /// <para>
    /// Re-linking the same user is a no-op success (idempotent, so a retried request cannot
    /// fail). Re-pointing a linked driver at a <i>different</i> user is a conflict: unlink first,
    /// so both halves land in the audit trail as separate events.
    /// </para>
    /// </summary>
    public Result LinkUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure(DriverErrors.UserIdRequired);
        }

        if (UserId == userId)
        {
            // No state change, so no event and no write — leaving UpdatedAtUtc alone also keeps
            // this clear of the eventless-write guard.
            return Result.Success();
        }

        if (UserId is not null)
        {
            return Result.Failure(DriverErrors.AlreadyLinkedToAnotherUser);
        }

        UserId = userId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new DriverUserLinkedDomainEvent(Id, userId));
        return Result.Success();
    }

    /// <summary>
    /// Breaks the link, so the account can no longer act as this driver. The roster row and all
    /// its compliance history stay — this revokes access, it does not retire a driver (that is
    /// <see cref="ChangeStatus"/>).
    /// </summary>
    public Result UnlinkUser()
    {
        if (UserId is not { } previousUserId)
        {
            return Result.Failure(DriverErrors.NotLinked);
        }

        UserId = null;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new DriverUserUnlinkedDomainEvent(Id, previousUserId));
        return Result.Success();
    }

    private static Result ValidateDetails(string name, string licenceClass, string source)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(DriverErrors.NameRequired);
        }

        if (string.IsNullOrWhiteSpace(licenceClass))
        {
            return Result.Failure(DriverErrors.LicenceClassRequired);
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            return Result.Failure(DriverErrors.SourceRequired);
        }

        return Result.Success();
    }
}
