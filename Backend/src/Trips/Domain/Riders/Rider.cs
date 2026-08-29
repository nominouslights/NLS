using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Riders.Events;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Domain.Riders;

/// <summary>
/// A directory entry for a person who travels with Northern Link — built up automatically
/// from trip manifests (one entry per (service type, normalized name) within a tenant) so
/// dispatch can auto-fill future manifests and, for contract crew, track a rotation.
/// <para>
/// Dedup is name-based by design: <see cref="NormalizedName"/> (trimmed, whitespace
/// collapsed, upper-cased) is the key, while <see cref="Name"/> keeps the latest spelling
/// seen on a manifest. Identical names merge and misspellings fork — a real person-identity
/// model is future work. Riders exist only for passenger services (ContractCrew, Community,
/// Nihb, Charter); Cargo/Grocery manifests never reach this aggregate.
/// </para>
/// </summary>
public sealed class Rider : AggregateRoot, ITenantScoped
{
    /// <summary>The only crew rotations the operation runs — 5, 10, or 20 days on.</summary>
    public static readonly int[] AllowedRotationDays = [5, 10, 20];

    private Rider()
    {
        // EF Core materialization only.
        Name = null!;
        NormalizedName = null!;
    }

    public Guid TenantId { get; private set; }

    /// <summary>Display name — adopts the latest spelling recorded on a manifest.</summary>
    public string Name { get; private set; }

    /// <summary>Dedup key — <see cref="NormalizeName"/> applied to the display name.</summary>
    public string NormalizedName { get; private set; }

    public TripServiceType ServiceType { get; private set; }

    /// <summary>Latest known contact (email or phone) from the manifests — null when never given.</summary>
    public string? Contact { get; private set; }

    /// <summary>
    /// Crew rotation length in days — ContractCrew only, one of <see cref="AllowedRotationDays"/>,
    /// null when not set. Drives the read side's next-expected-travel date.
    /// </summary>
    public int? RotationDays { get; private set; }

    public DateOnly? LastTripDate { get; private set; }
    public string? LastTripNumber { get; private set; }
    public int TripCount { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<Rider> Create(
        Guid tenantId,
        string name,
        TripServiceType serviceType,
        string? contact,
        DateOnly tripDate,
        string tripNumber)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Rider>(RiderErrors.NameRequired);
        }

        var display = CollapseWhitespace(name);
        var now = DateTimeOffset.UtcNow;
        var rider = new Rider
        {
            TenantId = tenantId,
            Name = display,
            NormalizedName = NormalizeName(name),
            ServiceType = serviceType,
            Contact = Clean(contact),
            RotationDays = null,
            LastTripDate = tripDate,
            LastTripNumber = tripNumber.Trim(),
            TripCount = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        rider.Raise(new RiderCreatedDomainEvent(rider.Id));
        return Result.Success(rider);
    }

    /// <summary>
    /// Records that this rider appeared on a manifest. Idempotent for the at-least-once
    /// upsert pipeline: <see cref="TripCount"/> only advances when the trip number differs
    /// from <see cref="LastTripNumber"/> (redelivery of the same manifest converges), the
    /// latest-trip fields (name spelling, contact, date, number) are adopted only when
    /// <paramref name="tripDate"/> is not older than <see cref="LastTripDate"/> (a
    /// backfilled older manifest never regresses them), and the event is raised only when
    /// something actually changed. A null/blank <paramref name="contact"/> never erases a
    /// known one — absence of contact info on one manifest isn't evidence it went away.
    /// </summary>
    public void RecordTrip(string displayName, string? contact, DateOnly tripDate, string tripNumber)
    {
        var changed = false;
        var number = tripNumber.Trim();

        if (!string.Equals(LastTripNumber, number, StringComparison.OrdinalIgnoreCase))
        {
            TripCount++;
            changed = true;
        }

        if (LastTripDate is null || tripDate >= LastTripDate)
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                var display = CollapseWhitespace(displayName);
                if (!string.Equals(Name, display, StringComparison.Ordinal))
                {
                    Name = display;
                    changed = true;
                }
            }

            if (Clean(contact) is { } cleaned && !string.Equals(Contact, cleaned, StringComparison.Ordinal))
            {
                Contact = cleaned;
                changed = true;
            }

            if (LastTripDate != tripDate)
            {
                LastTripDate = tripDate;
                changed = true;
            }

            if (!string.Equals(LastTripNumber, number, StringComparison.Ordinal))
            {
                LastTripNumber = number;
                changed = true;
            }
        }

        if (changed)
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow;
            Raise(new RiderTripRecordedDomainEvent(Id));
        }
    }

    /// <summary>
    /// Sets or clears the crew rotation. A non-null value is only valid for a ContractCrew
    /// rider and must be one of <see cref="AllowedRotationDays"/>; null always clears.
    /// Setting the value it already has is a no-op (no event).
    /// </summary>
    public Result SetRotation(int? days)
    {
        if (days is { } value)
        {
            if (ServiceType != TripServiceType.ContractCrew)
            {
                return Result.Failure(RiderErrors.RotationNotApplicable);
            }

            if (!AllowedRotationDays.Contains(value))
            {
                return Result.Failure(RiderErrors.InvalidRotation);
            }
        }

        if (RotationDays == days)
        {
            return Result.Success();
        }

        RotationDays = days;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new RiderRotationChangedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// The directory's dedup key for a passenger name: trimmed, internal whitespace runs
    /// collapsed to single spaces, upper-cased invariantly ("  mary  Beardy " → "MARY BEARDY").
    /// </summary>
    public static string NormalizeName(string name) =>
        CollapseWhitespace(name).ToUpperInvariant();

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
