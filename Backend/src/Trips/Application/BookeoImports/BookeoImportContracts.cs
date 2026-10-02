namespace NorthernLink.Trips.Application.BookeoImports;

// The API contract of /api/trips/imports/bookeo — serialized camelCase. The Dispatcher's
// BookeoImportModal is built against exactly these shapes (plan: bookeo-import.md); change a
// name here and the frontend breaks. Dates are "yyyy-MM-dd" strings and times "HH:mm" strings
// (not TimeOnly, which would serialize as "HH:mm:ss"). Enum-like fields travel as their names.
// No member anywhere in this file carries a tax figure, by rule.

/// <summary><c>{ code, severity: "Block" | "Warning" | "Info", message }</c>.</summary>
public sealed record ImportIssue(string Code, string Severity, string Message);

public sealed record BookeoImportPreview(
    Guid BatchId,
    string FileName,
    DateTimeOffset UploadedAtUtc,
    string PlanHash,
    BookeoImportSummary Summary,
    IReadOnlyList<BookeoUnmappedProduct> UnmappedProducts,
    IReadOnlyList<BookeoUnmatchedUnit> UnmatchedUnits,
    IReadOnlyList<BookeoImportGroup> Groups,
    IReadOnlyList<BookeoImportRow> Rows);

public sealed record BookeoImportSummary(
    int Rows,
    int New,
    int Changed,
    int Cancelled,
    int Unchanged,
    int Skipped,
    int TripsToCreate,
    int TripsToUpdate,
    int TripsToCancel,
    int BlockedGroups,
    int Warnings);

public sealed record BookeoUnmappedProduct(string ProductCode, string ProductName, string? Destination, int RowCount);

public sealed record BookeoUnmatchedUnit(string UnitText, int RowCount);

public sealed record BookeoImportGroup(
    string Key,
    Guid? RouteId,
    string? RouteName,
    string? Direction,
    string ServiceDate,
    string WindowStart,
    string? WindowEnd,
    string Action,
    Guid? ExistingTripId,
    string? ExistingTripNumber,
    BookeoGroupVehicle Vehicle,
    string? DriverName,
    int PassengersBefore,
    int PassengersAfter,
    int? SeatsCapacity,
    IReadOnlyList<string> BookingNumbers,
    IReadOnlyList<ImportIssue> Issues);

/// <summary><c>match</c>: "Matched" | "Unmatched" | "Ambiguous" | "Blank" | "KeptExisting".</summary>
public sealed record BookeoGroupVehicle(string? UnitText, Guid? VehicleId, string? VehicleUnit, string Match);

public sealed record BookeoImportRow(
    string BookingNumber,
    string CustomerName,
    string ProductName,
    string? Destination,
    string? ServiceDate,
    string? WindowStart,
    int Participants,
    string BookeoStatus,
    decimal TotalGrossCad,
    decimal TotalPaidCad,
    decimal TotalDueCad,
    string Action,
    IReadOnlyList<string> ChangedFields,
    string? GroupKey,
    IReadOnlyList<ImportIssue> Issues);

public sealed record BookeoImportCommitResult(
    Guid BatchId,
    int TripsCreated,
    int TripsUpdated,
    int TripsCancelled,
    int BookingsImported,
    int BookingsCancelled,
    int GroupsSkippedBlocked,
    IReadOnlyList<string> CreatedTripNumbers);

public sealed record BookeoProductMappingResponse(
    Guid Id,
    string ProductCode,
    string ProductName,
    string? Destination,
    Guid RouteId,
    string RouteName,
    string? Direction,
    string ResidentStopRole);

public sealed record BookeoUnitMappingResponse(Guid Id, string UnitText, Guid VehicleId, string? VehicleUnit);

public sealed record BookeoImportBatchResponse(
    Guid BatchId,
    string FileName,
    string UploadedBy,
    DateTimeOffset UploadedAtUtc,
    DateTimeOffset? CommittedAtUtc,
    string? CommittedBy,
    BookeoImportSummary? Summary);

/// <summary>Issue severities and the codes the planner raises — the contract's vocabulary.</summary>
public static class BookeoIssueCodes
{
    public const string Block = "Block";
    public const string Warning = "Warning";
    public const string Info = "Info";

    // Block.
    public const string ProductNotMapped = "ProductNotMapped";
    public const string RowUnreadable = "RowUnreadable";
    public const string ManifestCapExceeded = "ManifestCapExceeded";
    public const string OverVehicleCapacity = "OverVehicleCapacity";
    public const string TripNotEditable = "TripNotEditable";

    // Warning.
    public const string VehicleUnmatched = "VehicleUnmatched";
    public const string VehicleAmbiguous = "VehicleAmbiguous";
    public const string VehicleNotActive = "VehicleNotActive";
    public const string VehicleDoubleBooked = "VehicleDoubleBooked";
    public const string DriverDoubleBooked = "DriverDoubleBooked";
    public const string PossibleDuplicateTrip = "PossibleDuplicateTrip";
    public const string PaymentDue = "PaymentDue";
    public const string BookingCancelled = "BookingCancelled";
    public const string PastDeparture = "PastDeparture";
    public const string PassengerDoubleBooked = "PassengerDoubleBooked";
    public const string PassengerPlaceholder = "PassengerPlaceholder";
    public const string StopUnmatched = "StopUnmatched";
    public const string TripWillBeCancelled = "TripWillBeCancelled";

    // Info.
    public const string BookingChanged = "BookingChanged";
    public const string CanceledNeverImported = "CanceledNeverImported";
}

/// <summary>Row actions ("New" | "Changed" | "Cancelled" | "Unchanged" | "Skipped").</summary>
public enum BookeoRowAction
{
    New,
    Changed,
    Cancelled,
    Unchanged,
    Skipped,
}

/// <summary>Group actions ("Create" | "Update" | "Unchanged" | "Cancel" | "Blocked").</summary>
public enum BookeoGroupAction
{
    Create,
    Update,
    Unchanged,
    Cancel,
    Blocked,
}

/// <summary>Vehicle match outcomes.</summary>
public enum BookeoVehicleMatch
{
    Matched,
    Unmatched,
    Ambiguous,
    Blank,
    KeptExisting,
}
