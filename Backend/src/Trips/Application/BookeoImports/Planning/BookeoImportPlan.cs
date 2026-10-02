using System.Globalization;
using NorthernLink.Trips.Application.BookeoImports.Parsing;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.BookeoImports;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.BookeoImports.Planning;

/// <summary>
/// Everything the planner reads: the parsed rows, the ledger entries for their booking numbers,
/// the mappings, the fleet replica, the routes the mappings name, the trips that could be touched
/// (every trip on a service date in the file, plus every ledger trip) and their manifests.
/// <see cref="Now"/> only drives the <c>PastDeparture</c> warning, which the plan hash ignores.
/// </summary>
public sealed record BookeoPlanInput(
    IReadOnlyList<BookeoParsedRow> Rows,
    IReadOnlyDictionary<string, BookeoBooking> Ledger,
    IReadOnlyList<BookeoProductMapping> ProductMappings,
    IReadOnlyList<BookeoUnitMapping> UnitMappings,
    IReadOnlyList<VehicleLookup> Vehicles,
    IReadOnlyDictionary<Guid, Route> Routes,
    IReadOnlyList<Trip> Trips,
    IReadOnlyDictionary<Guid, TripManifest> ManifestsByTripId,
    DateTimeOffset Now);

/// <summary>The planner's result: the preview's content plus what a commit needs to apply it.</summary>
public sealed class BookeoImportPlan
{
    public required IReadOnlyList<PlannedRow> Rows { get; init; }
    public required IReadOnlyList<PlannedGroup> Groups { get; init; }
    public required BookeoImportSummary Summary { get; init; }
    public required IReadOnlyList<BookeoUnmappedProduct> UnmappedProducts { get; init; }
    public required IReadOnlyList<BookeoUnmatchedUnit> UnmatchedUnits { get; init; }

    /// <summary>
    /// SHA-256 over everything a commit would do — row actions and content hashes, group actions,
    /// target trips and their versions, passenger counts, vehicle decisions and issue codes (all
    /// but the clock-driven <c>PastDeparture</c>). A commit recomputes it; any difference is
    /// <c>PreviewStale</c>.
    /// </summary>
    public required string Hash { get; init; }

    public BookeoImportPreview ToPreview(Guid batchId, string fileName, DateTimeOffset uploadedAtUtc) => new(
        batchId,
        fileName,
        uploadedAtUtc,
        Hash,
        Summary,
        UnmappedProducts,
        UnmatchedUnits,
        Groups.Select(g => g.ToResponse()).ToList(),
        Rows.Select(r => r.ToResponse()).ToList());
}

/// <summary>One file row as the plan sees it.</summary>
public sealed class PlannedRow
{
    public required BookeoParsedRow Parsed { get; init; }
    public required BookeoRowAction Action { get; init; }
    public IReadOnlyList<string> ChangedFields { get; init; } = [];

    /// <summary>The booking's importable content — null only on an unreadable row.</summary>
    public BookeoBookingSnapshot? Snapshot { get; init; }

    public int PlaceholderCount { get; init; }
    public BookeoBooking? Ledger { get; init; }
    public BookeoProductMapping? Mapping { get; set; }
    public Route? Route { get; set; }

    /// <summary>The group whose trip this booking's passengers go onto (New/Changed), or sit on (Unchanged).</summary>
    public string? GroupKey { get; set; }

    /// <summary>The group of the ledger trip this booking's rows are removed from (Cancelled, or moved).</summary>
    public string? RemovalGroupKey { get; set; }

    /// <summary>The manifest rows this booking contributes to its group's trip.</summary>
    public IReadOnlyList<ManifestPassenger> ManifestRows { get; set; } = [];

    public List<ImportIssue> Issues { get; } = [];

    /// <summary>Not applied, not written to the ledger — it comes back on the next upload.</summary>
    public bool Blocked { get; set; }

    public string BookingNumber => Parsed.BookingNumber ?? $"row {Parsed.RowNumber}";

    public string ExternalRef => ManifestPassenger.BookeoRef(BookingNumber);

    /// <summary>Same departure as the ledger says: date, start, product and destination unchanged.</summary>
    public bool KeepsLedgerDeparture =>
        Ledger is not null && Snapshot is not null
        && Ledger.ServiceDate == Snapshot.ServiceDate
        && Ledger.WindowStart == Snapshot.WindowStart
        && Ledger.ProductCode == Snapshot.ProductCode
        && BookeoText.SameDestination(Ledger.Destination, Snapshot.Destination);

    public BookeoImportRow ToResponse() => new(
        BookingNumber,
        Parsed.CustomerName,
        Parsed.ProductName ?? Parsed.ProductCode ?? string.Empty,
        Parsed.Destination,
        Parsed.ServiceDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Parsed.WindowStart?.ToString("HH:mm", CultureInfo.InvariantCulture),
        Parsed.Participants,
        Parsed.Status,
        Parsed.TotalGrossCad,
        Parsed.TotalPaidCad,
        Parsed.TotalDueCad,
        Action.ToString(),
        ChangedFields,
        GroupKey ?? RemovalGroupKey,
        Issues);
}

/// <summary>One trip-to-be (Create) or existing trip (Update/Cancel/Unchanged), or a blocked bucket.</summary>
public sealed class PlannedGroup
{
    public required string Key { get; init; }
    public Guid? RouteId { get; init; }
    public Route? Route { get; init; }
    public string? RouteName { get; init; }
    public TripDirection? Direction { get; init; }
    public DateOnly ServiceDate { get; init; }
    public TimeOnly WindowStart { get; init; }
    public TimeOnly? WindowEnd { get; set; }

    /// <summary>A bucket of rows whose product has no mapping — always Blocked.</summary>
    public bool IsUnmapped { get; init; }

    public string? UnmappedProductCode { get; init; }

    /// <summary>The existing trip this group writes to; null means Create.</summary>
    public Trip? Target { get; set; }

    public TripManifest? Manifest { get; set; }

    public List<PlannedRow> AddRows { get; } = [];
    public List<PlannedRow> RemoveRows { get; } = [];

    /// <summary>Unchanged bookings already on this departure — shown, never written.</summary>
    public List<PlannedRow> MemberRows { get; } = [];

    // ---- Computed by the planner ----
    public BookeoGroupAction Action { get; set; }
    public int PassengersBefore { get; set; }
    public int PassengersAfter { get; set; }
    public int SeatsConfirmedAfter { get; set; }
    public int? SeatsCapacity { get; set; }
    public string? UnitText { get; set; }
    public BookeoVehicleMatch VehicleMatch { get; set; }
    public VehicleLookup? MatchedVehicle { get; set; }

    /// <summary>The vehicle the commit will put on the trip (create, or an unassigned existing trip).</summary>
    public VehicleLookup? AssignVehicle { get; set; }

    /// <summary>The manifest's passenger list after the commit (manual rows untouched).</summary>
    public IReadOnlyList<ManifestPassenger> PassengersAfterList { get; set; } = [];

    /// <summary>How many imported rows the commit removes from / adds to the trip — the seat-count delta.</summary>
    public int ImportedRowsRemoved { get; set; }

    public int ImportedRowsAdded { get; set; }

    /// <summary>The add/remove rows actually applied (blocked bookings excluded).</summary>
    public List<PlannedRow> EffectiveAdds { get; } = [];

    public List<PlannedRow> EffectiveRemoves { get; } = [];

    public List<ImportIssue> Issues { get; } = [];

    public bool HasOps => EffectiveAdds.Count > 0 || EffectiveRemoves.Count > 0;

    public bool IsBlocked => Issues.Any(i => i.Severity == BookeoIssueCodes.Block);

    public IEnumerable<string> BookingNumbers =>
        AddRows.Concat(RemoveRows).Concat(MemberRows)
            .Select(r => r.BookingNumber)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

    public BookeoImportGroup ToResponse() => new(
        Key,
        RouteId,
        RouteName,
        Direction?.ToString(),
        ServiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        WindowStart.ToString("HH:mm", CultureInfo.InvariantCulture),
        WindowEnd?.ToString("HH:mm", CultureInfo.InvariantCulture),
        Action.ToString(),
        Target?.Id,
        Target?.TripNumber,
        new BookeoGroupVehicle(
            UnitText,
            VehicleMatch == BookeoVehicleMatch.KeptExisting ? Target?.VehicleId : MatchedVehicle?.VehicleId,
            VehicleMatch == BookeoVehicleMatch.KeptExisting ? Target?.VehicleUnit : MatchedVehicle?.UnitNumber,
            VehicleMatch.ToString()),
        Target?.DriverName,
        PassengersBefore,
        PassengersAfter,
        SeatsCapacity,
        BookingNumbers.ToList(),
        Issues);
}
