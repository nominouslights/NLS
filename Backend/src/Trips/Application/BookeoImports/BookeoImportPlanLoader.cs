using System.Text.Json;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.BookeoImports.Parsing;
using NorthernLink.Trips.Application.BookeoImports.Planning;

namespace NorthernLink.Trips.Application.BookeoImports;

/// <summary>
/// Gathers the database state <see cref="BookeoImportPlanner"/> needs for a set of parsed rows and
/// runs it — the one path both the preview and the commit take, which is what makes the commit's
/// recomputed plan hash comparable to the preview's. A plain scoped service over the Application
/// abstractions (no EF here), like <c>ScheduleTripMaterializer</c>.
/// </summary>
public sealed class BookeoImportPlanLoader(IBookeoImportRepository repository, TimeProvider clock)
{
    public async Task<BookeoImportPlan> PlanAsync(
        IReadOnlyList<BookeoParsedRow> rows, CancellationToken cancellationToken)
    {
        var bookingNumbers = rows
            .Select(r => r.BookingNumber)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var ledger = await repository.GetBookingsAsync(bookingNumbers, cancellationToken);
        var productMappings = await repository.GetProductMappingsAsync(cancellationToken);
        var unitMappings = await repository.GetUnitMappingsAsync(cancellationToken);
        var vehicles = await repository.GetVehiclesAsync(cancellationToken);

        var serviceDates = rows
            .Select(r => r.ServiceDate)
            .OfType<DateOnly>()
            .Concat(ledger.Select(b => b.ServiceDate))
            .Distinct()
            .ToList();
        var ledgerTripIds = ledger.Select(b => b.TripId).OfType<Guid>().Distinct().ToList();
        var trips = await repository.GetTripsAsync(serviceDates, ledgerTripIds, cancellationToken);

        var routeIds = productMappings.Select(m => m.RouteId)
            .Concat(trips.Select(t => t.RouteId).OfType<Guid>())
            .Distinct()
            .ToList();
        var routes = await repository.GetRoutesAsync(routeIds, cancellationToken);
        var manifests = await repository.GetManifestsForTripsAsync(trips, cancellationToken);

        return BookeoImportPlanner.Plan(new BookeoPlanInput(
            rows,
            ledger.ToDictionary(b => b.BookingNumber, StringComparer.Ordinal),
            productMappings,
            unitMappings,
            vehicles,
            routes.ToDictionary(r => r.Id),
            trips,
            manifests,
            clock.GetUtcNow()));
    }
}

/// <summary>The serializer for the import's jsonb payloads (parsed rows, summary) — web defaults, camelCase.</summary>
public static class BookeoImportJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string SerializeRows(IReadOnlyList<BookeoParsedRow> rows) => JsonSerializer.Serialize(rows, Options);

    public static IReadOnlyList<BookeoParsedRow> DeserializeRows(string json) =>
        JsonSerializer.Deserialize<List<BookeoParsedRow>>(json, Options) ?? [];

    public static string SerializeSummary(BookeoImportSummary summary) => JsonSerializer.Serialize(summary, Options);

    public static BookeoImportSummary? DeserializeSummary(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<BookeoImportSummary>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
