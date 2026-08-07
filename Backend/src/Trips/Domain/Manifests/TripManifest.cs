using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Manifests.Events;

namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>
/// A route-aware passenger + cargo manifest for a trip. Since inspections detached from
/// the manifest (their pre/post checklists, odometer, fuel, conditions, issues, and
/// certification now live in Fleet's <c>VehicleInspection</c>), this aggregate keeps only
/// §1 trip info, §5 passengers, and §6 cargo. Unlike the old create-whole form, a manifest
/// is <b>editable any time without changing trip status</b>: <see cref="Create"/> records
/// it and <see cref="Update"/> revises it, each raising a journaled domain event stamped
/// with <see cref="Source"/> + <see cref="EnteredBy"/> — that event stream is the audit
/// log. Passengers reference the trip's route stops by snapshot; the row collections
/// (§5/§6) persist as jsonb.
/// </summary>
public sealed class TripManifest : AggregateRoot, ITenantScoped
{
    private TripManifest()
    {
        // EF Core materialization only.
        TripNumber = null!;
        Route = null!;
    }

    public Guid TenantId { get; private set; }

    // §1 — trip information.
    public DateOnly TripDate { get; private set; }
    public string TripNumber { get; private set; }
    public string Route { get; private set; }
    public TripDirection? Direction { get; private set; }
    public string? Client { get; private set; }

    // §5 — passengers (max 8).
    public List<ManifestPassenger> Passengers { get; private set; } = [];
    public bool AllSeatbeltsVerified { get; private set; }

    // §6 — cargo (max 4).
    public List<ManifestCargoItem> Cargo { get; private set; } = [];
    public CargoSecuredStatus? AllCargoSecured { get; private set; }

    // Provenance — stamped on create and re-stamped on every edit.
    public ManifestSource Source { get; private set; }
    public string? EnteredBy { get; private set; }
    public DateTimeOffset? EnteredAt { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// Fares actually collected on this run — cash and online, waived seats excluded. Computed
    /// from the passenger rows rather than stored, for the same reason an invoice's subtotal is:
    /// a total that can disagree with the lines under it is worse than no total.
    /// <para>
    /// Nothing downstream consumes this yet. It is not reconciled against a bank deposit or a
    /// QuickBooks receipt, so it answers "what did the driver write down", not "what did we
    /// bank" — the screen says so, and the QBO path is its own change.
    /// </para>
    /// </summary>
    public decimal FaresCollectedCad => Math.Round(
        Passengers
            .Where(p => p.FarePaymentMethod is Manifests.FarePaymentMethod.Cash
                or Manifests.FarePaymentMethod.Online)
            .Sum(p => p.FareAmountCad ?? 0m),
        2);

    /// <summary>Fares deliberately not charged — recorded so a waived seat is a decision, not a gap.</summary>
    public int FaresWaivedCount =>
        Passengers.Count(p => p.FarePaymentMethod is Manifests.FarePaymentMethod.Waived);

    /// <summary>How many passengers actually paid something.</summary>
    public int FaresPaidCount => Passengers.Count(p =>
        p.FarePaymentMethod is Manifests.FarePaymentMethod.Cash or Manifests.FarePaymentMethod.Online);

    /// <summary>
    /// Records a manifest. Valid with just §1 trip info plus zero-or-more passengers and
    /// cargo — no inspection, attestation, or signature requirements anymore. A
    /// dispatcher-entered manifest must say who entered it.
    /// </summary>
    public static Result<TripManifest> Create(
        Guid tenantId,
        DateOnly tripDate,
        string tripNumber,
        string route,
        TripDirection? direction,
        string? client,
        IReadOnlyList<ManifestPassenger> passengers,
        bool allSeatbeltsVerified,
        IReadOnlyList<ManifestCargoItem> cargo,
        CargoSecuredStatus? allCargoSecured,
        ManifestSource source,
        string? enteredBy)
    {
        var validation = Validate(tripNumber, passengers, cargo, source, enteredBy);
        if (validation.IsFailure)
        {
            return Result.Failure<TripManifest>(validation.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var manifest = new TripManifest
        {
            TenantId = tenantId,
            TripDate = tripDate,
            TripNumber = tripNumber.Trim(),
            Route = route?.Trim() ?? string.Empty,
            Direction = direction,
            Client = Normalize(client),
            Passengers = [.. passengers],
            AllSeatbeltsVerified = allSeatbeltsVerified,
            Cargo = [.. cargo],
            AllCargoSecured = allCargoSecured,
            Source = source,
            EnteredBy = StampEnteredBy(source, enteredBy),
            EnteredAt = source == ManifestSource.Dispatcher ? now : null,
            CreatedAtUtc = now,
        };

        manifest.Raise(new TripManifestCreatedDomainEvent(manifest.Id));
        return Result.Success(manifest);
    }

    /// <summary>
    /// Revises §1 trip info, passengers, and cargo in place — any time, without touching
    /// trip status. Re-stamps provenance from the editing <paramref name="source"/> /
    /// <paramref name="enteredBy"/> and raises the journaled
    /// <see cref="TripManifestUpdatedDomainEvent"/> (the audit entry for this edit).
    /// </summary>
    public Result Update(
        DateOnly tripDate,
        string tripNumber,
        string route,
        TripDirection? direction,
        string? client,
        IReadOnlyList<ManifestPassenger> passengers,
        bool allSeatbeltsVerified,
        IReadOnlyList<ManifestCargoItem> cargo,
        CargoSecuredStatus? allCargoSecured,
        ManifestSource source,
        string? enteredBy)
    {
        var validation = Validate(tripNumber, passengers, cargo, source, enteredBy);
        if (validation.IsFailure)
        {
            return validation;
        }

        TripDate = tripDate;
        TripNumber = tripNumber.Trim();
        Route = route?.Trim() ?? string.Empty;
        Direction = direction;
        Client = Normalize(client);
        Passengers = [.. passengers];
        AllSeatbeltsVerified = allSeatbeltsVerified;
        Cargo = [.. cargo];
        AllCargoSecured = allCargoSecured;
        Source = source;
        EnteredBy = StampEnteredBy(source, enteredBy);
        EnteredAt = DateTimeOffset.UtcNow;

        Raise(new TripManifestUpdatedDomainEvent(Id, source, EnteredBy));
        return Result.Success();
    }

    private static Result Validate(
        string tripNumber,
        IReadOnlyList<ManifestPassenger> passengers,
        IReadOnlyList<ManifestCargoItem> cargo,
        ManifestSource source,
        string? enteredBy)
    {
        if (string.IsNullOrWhiteSpace(tripNumber))
        {
            return Result.Failure(TripManifestErrors.TripNumberRequired);
        }

        if (passengers.Count > ManifestChecklist.MaxPassengers)
        {
            return Result.Failure(TripManifestErrors.TooManyPassengers);
        }

        if (cargo.Count > ManifestChecklist.MaxCargoItems)
        {
            return Result.Failure(TripManifestErrors.TooManyCargoItems);
        }

        if (passengers.Any(p => string.IsNullOrWhiteSpace(p.Name)))
        {
            return Result.Failure(TripManifestErrors.PassengerNameRequired);
        }

        if (source == ManifestSource.Dispatcher && string.IsNullOrWhiteSpace(enteredBy))
        {
            return Result.Failure(TripManifestErrors.EnteredByRequired);
        }

        return ValidateFares(passengers);
    }

    /// <summary>
    /// Fares are optional — a contract run has none — but a half-recorded one is worse than
    /// nothing: an amount with no method can't be reconciled, and a method with no amount reads
    /// as unpaid. Rounding is checked here because jsonb enforces no scale of its own.
    /// </summary>
    private static Result ValidateFares(IReadOnlyList<ManifestPassenger> passengers)
    {
        foreach (var passenger in passengers)
        {
            if (passenger.FareAmountCad is { } amount
                && (amount < 0m || decimal.Round(amount, 2) != amount))
            {
                return Result.Failure(TripManifestErrors.InvalidFareAmount);
            }

            switch (passenger.FarePaymentMethod)
            {
                case null when passenger.FareAmountCad is > 0m || passenger.FarePaidAtUtc is not null:
                    return Result.Failure(TripManifestErrors.FarePaymentMethodRequired);

                case Manifests.FarePaymentMethod.Cash or Manifests.FarePaymentMethod.Online
                    when passenger.FareAmountCad is null or <= 0m:
                    return Result.Failure(TripManifestErrors.FareAmountRequired);

                case Manifests.FarePaymentMethod.Waived when passenger.FareAmountCad is > 0m:
                    return Result.Failure(TripManifestErrors.WaivedFareMustBeZero);
            }
        }

        return Result.Success();
    }

    private static string? StampEnteredBy(ManifestSource source, string? enteredBy) =>
        source == ManifestSource.Dispatcher ? enteredBy!.Trim() : Normalize(enteredBy);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
