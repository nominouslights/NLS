using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Manifests.Events;

namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>
/// A completed NL-TM-01 Trip Manifest &amp; Vehicle Inspection Report — all ten sections
/// of the paper form as one aggregate. Manifests are only ever created whole (there is
/// no draft state): the driver certifies on the device, or a dispatcher transcribes a
/// finished paper copy, so <see cref="Create"/> is the single entry point and the only
/// place the form's invariants are enforced. Row collections (§3, §5, §6, §9) persist
/// as jsonb. Provenance distinguishes App submissions from paper transcriptions.
/// </summary>
public sealed class TripManifest : AggregateRoot, ITenantScoped
{
    private TripManifest()
    {
        // EF Core materialization only.
        TripNumber = null!;
        Route = null!;
        Unit = null!;
        DriverName = null!;
        DriverSignatureName = null!;
    }

    public Guid TenantId { get; private set; }

    // §1 — trip information.
    public DateOnly TripDate { get; private set; }
    public string TripNumber { get; private set; }
    public string Route { get; private set; }
    public TripDirection? Direction { get; private set; }
    public string? Client { get; private set; }

    // §2 — vehicle & driver.
    public string Unit { get; private set; }
    public string DriverName { get; private set; }
    public string? DriverLicenceNo { get; private set; }
    public string? LicencePlate { get; private set; }
    public int? OdometerStartKm { get; private set; }
    public FuelLevel? FuelLevel { get; private set; }

    // §3 — pre-trip inspection (28 canonical items).
    public List<PreTripChecklistItem> PreTripItems { get; private set; } = [];

    // §4 — weather & road conditions.
    public List<WeatherCondition> Weather { get; private set; } = [];
    public string? TemperatureC { get; private set; }
    public List<RoadCondition> RoadConditions { get; private set; } = [];
    public VisibilityLevel? Visibility { get; private set; }
    public string? RoadAdvisories { get; private set; }

    // §5 — passengers (max 8).
    public List<ManifestPassenger> Passengers { get; private set; } = [];
    public bool AllSeatbeltsVerified { get; private set; }

    // §6 — cargo (max 4).
    public List<ManifestCargoItem> Cargo { get; private set; } = [];
    public CargoSecuredStatus? AllCargoSecured { get; private set; }

    // §7 — issues log.
    public List<string> Issues { get; private set; } = [];
    public bool NoIssues { get; private set; }

    // §8 — trip completion log.
    public string? DepartureTime { get; private set; }
    public string? ArrivalTime { get; private set; }
    public int? OdometerEndKm { get; private set; }
    public int? TotalKm { get; private set; }
    public bool FuelAdded { get; private set; }
    public decimal? FuelLitres { get; private set; }
    public decimal? FuelCostCad { get; private set; }

    // §9 — post-trip check (6 items).
    public List<PostTripChecklistItem> PostTripItems { get; private set; } = [];

    // §10 — certification.
    public List<bool> Attestations { get; private set; } = [];
    public string DriverSignatureName { get; private set; }
    public DateTimeOffset CertifiedAt { get; private set; }

    // Provenance.
    public ManifestSource Source { get; private set; }
    public string? EnteredBy { get; private set; }
    public DateTimeOffset? EnteredAt { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<TripManifest> Create(
        Guid tenantId,
        DateOnly tripDate,
        string tripNumber,
        string route,
        TripDirection? direction,
        string? client,
        string unit,
        string driverName,
        string? driverLicenceNo,
        string? licencePlate,
        int? odometerStartKm,
        FuelLevel? fuelLevel,
        IReadOnlyList<PreTripChecklistItem> preTripItems,
        IReadOnlyList<WeatherCondition> weather,
        string? temperatureC,
        IReadOnlyList<RoadCondition> roadConditions,
        VisibilityLevel? visibility,
        string? roadAdvisories,
        IReadOnlyList<ManifestPassenger> passengers,
        bool allSeatbeltsVerified,
        IReadOnlyList<ManifestCargoItem> cargo,
        CargoSecuredStatus? allCargoSecured,
        IReadOnlyList<string> issues,
        bool noIssues,
        string? departureTime,
        string? arrivalTime,
        int? odometerEndKm,
        int? totalKm,
        bool fuelAdded,
        decimal? fuelLitres,
        decimal? fuelCostCad,
        IReadOnlyList<PostTripChecklistItem> postTripItems,
        IReadOnlyList<bool> attestations,
        string driverSignatureName,
        DateTimeOffset certifiedAt,
        ManifestSource source,
        string? enteredBy)
    {
        if (string.IsNullOrWhiteSpace(tripNumber))
        {
            return Result.Failure<TripManifest>(TripManifestErrors.TripNumberRequired);
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            return Result.Failure<TripManifest>(TripManifestErrors.UnitRequired);
        }

        if (string.IsNullOrWhiteSpace(driverName))
        {
            return Result.Failure<TripManifest>(TripManifestErrors.DriverNameRequired);
        }

        if (passengers.Count > ManifestChecklist.MaxPassengers)
        {
            return Result.Failure<TripManifest>(TripManifestErrors.TooManyPassengers);
        }

        if (cargo.Count > ManifestChecklist.MaxCargoItems)
        {
            return Result.Failure<TripManifest>(TripManifestErrors.TooManyCargoItems);
        }

        if (preTripItems.Any(item =>
                item.Status == PreTripItemStatus.Fail && string.IsNullOrWhiteSpace(item.Note)))
        {
            return Result.Failure<TripManifest>(TripManifestErrors.FailRequiresNote);
        }

        if (odometerStartKm is { } start && odometerEndKm is { } end && end < start)
        {
            return Result.Failure<TripManifest>(TripManifestErrors.OdometerEndBeforeStart);
        }

        if (fuelAdded && fuelLitres is null)
        {
            return Result.Failure<TripManifest>(TripManifestErrors.FuelLitresRequired);
        }

        if (attestations.Count != ManifestChecklist.AttestationCount || attestations.Contains(false))
        {
            return Result.Failure<TripManifest>(TripManifestErrors.AttestationsIncomplete);
        }

        if (string.IsNullOrWhiteSpace(driverSignatureName))
        {
            return Result.Failure<TripManifest>(TripManifestErrors.SignatureRequired);
        }

        if (source == ManifestSource.Paper && string.IsNullOrWhiteSpace(enteredBy))
        {
            return Result.Failure<TripManifest>(TripManifestErrors.EnteredByRequired);
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
            Unit = unit.Trim(),
            DriverName = driverName.Trim(),
            DriverLicenceNo = Normalize(driverLicenceNo),
            LicencePlate = Normalize(licencePlate),
            OdometerStartKm = odometerStartKm,
            FuelLevel = fuelLevel,
            PreTripItems = [.. preTripItems],
            Weather = [.. weather],
            TemperatureC = Normalize(temperatureC),
            RoadConditions = [.. roadConditions],
            Visibility = visibility,
            RoadAdvisories = Normalize(roadAdvisories),
            Passengers = [.. passengers],
            AllSeatbeltsVerified = allSeatbeltsVerified,
            Cargo = [.. cargo],
            AllCargoSecured = allCargoSecured,
            Issues = [.. issues],
            NoIssues = noIssues,
            DepartureTime = Normalize(departureTime),
            ArrivalTime = Normalize(arrivalTime),
            OdometerEndKm = odometerEndKm,
            TotalKm = totalKm,
            FuelAdded = fuelAdded,
            FuelLitres = fuelLitres,
            FuelCostCad = fuelCostCad,
            PostTripItems = [.. postTripItems],
            Attestations = [.. attestations],
            DriverSignatureName = driverSignatureName.Trim(),
            CertifiedAt = certifiedAt,
            Source = source,
            EnteredBy = source == ManifestSource.Paper ? enteredBy!.Trim() : Normalize(enteredBy),
            EnteredAt = source == ManifestSource.Paper ? now : null,
            CreatedAtUtc = now,
        };

        manifest.Raise(new TripManifestCompletedDomainEvent(manifest.Id));
        return Result.Success(manifest);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
