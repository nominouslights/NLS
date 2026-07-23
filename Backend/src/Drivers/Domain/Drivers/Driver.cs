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
