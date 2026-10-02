using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.BookeoImports;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>How a Bookeo import's single save ended.</summary>
public enum BookeoSaveOutcome
{
    Saved,

    /// <summary>The batch row's committed_at_utc was no longer null — another confirm won.</summary>
    BatchAlreadyCommitted,

    /// <summary>A trip/manifest changed underneath (version token) or a ledger booking number raced in (23505).</summary>
    Conflict,
}

/// <summary>
/// Persistence for the Bookeo import — the batches, the ledger and the two mapping tables, plus
/// the trip/manifest reads the planner needs. Every write the import makes, including the trips
/// and manifests it creates or edits, goes through ONE <see cref="SaveAsync"/> on the Trips
/// DbContext, so a commit is a single transaction. Tenant-scoped (query filter + Postgres RLS).
/// Entities returned are tracked: the commit mutates them in place.
/// </summary>
public interface IBookeoImportRepository
{
    void AddBatch(BookeoImportBatch batch);

    Task<BookeoImportBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookeoImportBatch>> GetRecentBatchesAsync(int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookeoBooking>> GetBookingsAsync(
        IReadOnlyCollection<string> bookingNumbers, CancellationToken cancellationToken = default);

    void AddBooking(BookeoBooking booking);

    Task<IReadOnlyList<BookeoProductMapping>> GetProductMappingsAsync(CancellationToken cancellationToken = default);

    void AddProductMapping(BookeoProductMapping mapping);

    void RemoveProductMapping(BookeoProductMapping mapping);

    Task<IReadOnlyList<BookeoUnitMapping>> GetUnitMappingsAsync(CancellationToken cancellationToken = default);

    void AddUnitMapping(BookeoUnitMapping mapping);

    void RemoveUnitMapping(BookeoUnitMapping mapping);

    Task<IReadOnlyList<VehicleLookup>> GetVehiclesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Route>> GetRoutesAsync(IReadOnlyCollection<Guid> routeIds, CancellationToken cancellationToken = default);

    /// <summary>Every trip on one of <paramref name="serviceDates"/>, plus the trips in <paramref name="tripIds"/>.</summary>
    Task<IReadOnlyList<Trip>> GetTripsAsync(
        IReadOnlyCollection<DateOnly> serviceDates,
        IReadOnlyCollection<Guid> tripIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The manifest of each trip — by <c>Trip.ManifestId</c>, else by trip number (manifests link
    /// to trips lazily, so a just-created one may not be stamped on its trip yet). Keyed by trip id.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, TripManifest>> GetManifestsForTripsAsync(
        IReadOnlyCollection<Trip> trips, CancellationToken cancellationToken = default);

    void AddTrip(Trip trip);

    void AddManifest(TripManifest manifest);

    /// <summary>One save, one transaction. Mapping races and version conflicts come back as outcomes, not exceptions.</summary>
    Task<BookeoSaveOutcome> SaveAsync(CancellationToken cancellationToken = default);
}
