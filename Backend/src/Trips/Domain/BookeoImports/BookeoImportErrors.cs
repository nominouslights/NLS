using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.BookeoImports;

/// <summary>
/// Every error the Bookeo booking-report import can return. Codes are part of the API contract
/// (<c>Trips.BookeoImport.*</c>) — the Dispatcher's import modal switches on them.
/// </summary>
public static class BookeoImportErrors
{
    /// <summary>The largest file accepted — a Bookeo export of a few hundred bookings is ~50 KB.</summary>
    public const long MaxFileBytes = 5 * 1024 * 1024;

    /// <summary>The most booking rows one upload may carry.</summary>
    public const int MaxRows = 2000;

    public static readonly Error FileRequired = Error.Validation(
        "Trips.BookeoImport.FileRequired", "Choose a Bookeo booking report (.xls or .xlsx) to upload in the 'file' field.");

    public static readonly Error FileTooLarge = Error.Validation(
        "Trips.BookeoImport.FileTooLarge", "The file is larger than 5 MB. Export a shorter date range from Bookeo.");

    public static Error TooManyRows(int rows) => Error.Validation(
        "Trips.BookeoImport.FileTooLarge",
        $"The report has {rows} booking rows; at most {MaxRows} can be imported at once. Export a shorter date range from Bookeo.");

    public static readonly Error UnsupportedFile = Error.Validation(
        "Trips.BookeoImport.UnsupportedFile", "Only Bookeo booking reports exported as .xls or .xlsx can be imported.");

    public static Error HeaderNotRecognized(IReadOnlyCollection<string> missing) => Error.Validation(
        "Trips.BookeoImport.HeaderNotRecognized",
        $"This does not look like a Bookeo booking report. Missing column(s): {string.Join(", ", missing)}.");

    public static readonly Error BatchNotFound = Error.NotFound(
        "Trips.BookeoImport.BatchNotFound", "The import preview was not found. Upload the file again.");

    public static readonly Error AlreadyCommitted = Error.Conflict(
        "Trips.BookeoImport.AlreadyCommitted", "This import has already been applied.");

    public static readonly Error PreviewStale = Error.Conflict(
        "Trips.BookeoImport.PreviewStale",
        "Trips, manifests or mappings changed since this preview was built. Review the refreshed preview and confirm again.");

    public static readonly Error PlanHashRequired = Error.Validation(
        "Trips.BookeoImport.PlanHashRequired", "Confirming an import requires the planHash from its preview.");

    public static readonly Error ProductCodeRequired = Error.Validation(
        "Trips.BookeoImport.ProductCodeRequired", "Every product mapping needs the Bookeo product code.");

    public static readonly Error RouteRequired = Error.Validation(
        "Trips.BookeoImport.RouteRequired", "Every product mapping needs a route.");

    public static readonly Error RouteNotFound = Error.Validation(
        "Trips.BookeoImport.RouteNotFound", "A product mapping names a route that does not exist.");

    public static readonly Error UnitTextRequired = Error.Validation(
        "Trips.BookeoImport.UnitTextRequired", "Every unit mapping needs the Bookeo unit text.");

    public static readonly Error VehicleNotFound = Error.Validation(
        "Trips.BookeoImport.VehicleNotFound", "A unit mapping names a vehicle that is not in the fleet.");

    public static readonly Error DuplicateMapping = Error.Validation(
        "Trips.BookeoImport.DuplicateMapping", "The same product/destination or unit appears twice in one save.");

    public static readonly Error MappingConflict = Error.Conflict(
        "Trips.BookeoImport.MappingConflict", "Someone else saved the same mapping at the same time. Reload and try again.");

    public static readonly Error MappingNotFound = Error.NotFound(
        "Trips.BookeoImport.MappingNotFound", "The mapping was not found.");
}
