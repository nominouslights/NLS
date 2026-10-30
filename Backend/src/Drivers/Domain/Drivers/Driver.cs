using NorthernLink.Drivers.Domain.Drivers.Events;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Drivers;

/// <summary>
/// A driver on the roster — Northern Link's own or a partner's (<see cref="Source"/>
/// carries "Northern Link" or the partner name). Trips reference drivers by id via the
/// <c>drivers.driver-changed</c> integration event; credentials and client clearances
/// are separate aggregates keyed on <see cref="AggregateRoot.Id"/>. Credential expiry
/// chips (valid / expiring / expired) are derived by the frontend from the dates, so
/// nothing like that is stored here. Hours of service are their own aggregates keyed on
/// <see cref="AggregateRoot.Id"/> too: <c>DutyDay</c> (the duty-event ledger the rule engine
/// evaluates) and the legacy <c>HosLogEntry</c> daily totals.
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
    /// The prior-records attestation (s.82(1)(f)): a compliance officer confirms the driver's
    /// records from before their first day on this ledger are on file through
    /// <see cref="PriorRecordsCoveredThroughDate"/>. The hours ledger's known-from moves back to
    /// 13 days before that date, which is what clears <c>RecordsIncomplete</c> for a new hire
    /// whose previous carrier's records have been obtained. Null until attested.
    /// </summary>
    public DateOnly? PriorRecordsCoveredThroughDate { get; private set; }

    public DateTimeOffset? PriorRecordsAttestedAtUtc { get; private set; }
    public Guid? PriorRecordsAttestedByUserId { get; private set; }
    public string? PriorRecordsNote { get; private set; }

    /// <summary>Declared at attestation: the driver also drives for another carrier, so their hours there count (s.82).</summary>
    public bool WorksForOtherCarriers { get; private set; }

    public const int PriorRecordsNoteMaxLength = 1000;

    /// <summary>The regulation's day boundaries, America/Winnipeg — for "not after today".</summary>
    private static readonly HosCalendar DefaultCalendar = new();

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

    /// <summary>
    /// A compliance officer attests the driver's prior records are on file through
    /// <paramref name="coveredThrough"/> (not after today, America/Winnipeg). The handler proves
    /// the role. Re-attesting identical values is a no-op (no event, no write); any change —
    /// a later date, a different note, the other-carriers flag — is a fresh attestation by
    /// <paramref name="byUserId"/> at <paramref name="now"/>.
    /// </summary>
    public Result AttestPriorRecords(
        DateOnly coveredThrough,
        Guid byUserId,
        string? note,
        bool worksForOtherCarriers,
        DateTimeOffset now,
        HosCalendar? calendar = null)
    {
        if (byUserId == Guid.Empty)
        {
            return Result.Failure(DriverErrors.UserIdRequired);
        }

        if (coveredThrough > (calendar ?? DefaultCalendar).DayOf(now.ToUnixTimeSeconds()))
        {
            return Result.Failure(ComplianceErrors.PriorRecordsDateInFuture);
        }

        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (cleanNote is { Length: > PriorRecordsNoteMaxLength })
        {
            return Result.Failure(ComplianceErrors.PriorRecordsNoteTooLong);
        }

        if (PriorRecordsCoveredThroughDate == coveredThrough
            && WorksForOtherCarriers == worksForOtherCarriers
            && string.Equals(PriorRecordsNote, cleanNote, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        PriorRecordsCoveredThroughDate = coveredThrough;
        PriorRecordsAttestedAtUtc = now;
        PriorRecordsAttestedByUserId = byUserId;
        PriorRecordsNote = cleanNote;
        WorksForOtherCarriers = worksForOtherCarriers;
        UpdatedAtUtc = now;

        Raise(new DriverPriorRecordsAttestedDomainEvent(Id, coveredThrough, worksForOtherCarriers));
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
