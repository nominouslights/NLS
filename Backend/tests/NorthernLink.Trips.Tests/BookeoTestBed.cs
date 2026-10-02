using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.BookeoImports;
using NorthernLink.Trips.Application.BookeoImports.Commit;
using NorthernLink.Trips.Application.BookeoImports.Parsing;
using NorthernLink.Trips.Application.BookeoImports.Planning;
using NorthernLink.Trips.Application.BookeoImports.Preview;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.BookeoImports;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Infrastructure.BookeoImports;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// A Bookeo import wired over in-memory fakes: the Thompson ↔ Lynn Lake route (Thompson, Leaf
/// Rapids, Lynn Lake), the three synthetic fixture products mapped onto it, and van NL-02 (14
/// seats, Active) pinned to the fixture's "Ford Transit 150" unit text. The clock sits on
/// 2026-10-01, before every fixture departure.
/// </summary>
internal sealed class BookeoTestBed
{
    public static readonly Guid TenantId = TestPlanning.TenantId;

    public const string FromThompson = "41578WMAM3X1A0E69B7F59";
    public const string ToThompson = "41578HWK6CT1A0E4970BA2";
    public const string LakeToRapids = "41578CPNRWL1A0FA82032B";

    public static readonly Guid ThompsonStop = Guid.Parse("00000000-0000-0000-0000-0000000005a1");
    public static readonly Guid LeafRapidsStop = Guid.Parse("00000000-0000-0000-0000-0000000005a2");
    public static readonly Guid LynnLakeStop = Guid.Parse("00000000-0000-0000-0000-0000000005a3");
    public static readonly Guid VanId = Guid.Parse("00000000-0000-0000-0000-0000000005e2");

    public FakeBookeoImportRepository Repo { get; } = new();
    public FakeClock Clock { get; } = new(new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero));
    public FakeTripNumberGenerator Numbers { get; } = new();
    public Route Route { get; }
    public VehicleLookup Van { get; }

    public BookeoTestBed(bool mapProducts = true, bool mapUnit = true)
    {
        Route = Route.Create(
            TenantId,
            "Thompson ↔ Lynn Lake",
            [
                new RouteStop { StopId = ThompsonStop, Name = "Thompson", Order = 0 },
                new RouteStop { StopId = LeafRapidsStop, Name = "Leaf Rapids", Order = 1 },
                new RouteStop { StopId = LynnLakeStop, Name = "Lynn Lake", Order = 2 },
            ],
            distanceKm: 320,
            estimatedDuration: TimeSpan.FromMinutes(270),
            requiredLicenceClass: null).Value;
        Repo.Routes.Add(Route);

        Van = new VehicleLookup
        {
            VehicleId = VanId,
            TenantId = TenantId,
            UnitNumber = "NL-02",
            Status = VehicleLookup.ActiveStatus,
            RequiredLicenceClass = "Class 4",
            SeatingCapacity = 14,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        Repo.Vehicles.Add(Van);

        if (mapProducts)
        {
            Map(FromThompson, "Shuttle from Thompson", null, TripDirection.Outbound, ResidentStopRole.Dropoff);
            Map(ToThompson, "Shuttle to Thompson", null, TripDirection.Inbound, ResidentStopRole.Pickup);
            Map(LakeToRapids, "Lynn Lake <-> Leaf Rapids", "Leaf Rapids to Lynn Lake", TripDirection.Outbound, ResidentStopRole.Pickup);
        }

        if (mapUnit)
        {
            Repo.UnitMappings.Add(BookeoUnitMapping.Create(TenantId, "Ford Transit 150", VanId).Value);
        }
    }

    public BookeoProductMapping Map(
        string code, string name, string? destination, TripDirection? direction, ResidentStopRole role)
    {
        var mapping = BookeoProductMapping.Create(TenantId, code, name, destination, Route.Id, direction, role).Value;
        Repo.ProductMappings.Add(mapping);
        return mapping;
    }

    public static byte[] Fixture(string name = "bookeo_sample.xls") =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", name));

    /// <summary>The fixture's rows, parsed exactly as an upload would parse them.</summary>
    public static IReadOnlyList<BookeoParsedRow> FixtureRows(string name = "bookeo_sample.xls")
    {
        using var stream = new MemoryStream(Fixture(name));
        var grid = new ExcelBookeoWorkbookReader().Read(stream);
        return BookeoReportParser.Parse(grid.Value).Value;
    }

    public static BookeoParsedRow FixtureRow(string bookingNumberSuffix) =>
        FixtureRows().Single(r => r.BookingNumber!.EndsWith(bookingNumberSuffix, StringComparison.Ordinal));

    public BookeoImportPlanLoader Loader => new(Repo, Clock);

    public PreviewBookeoImportCommandHandler PreviewHandler =>
        new(new ExcelBookeoWorkbookReader(), Loader, Repo, Clock);

    public CommitBookeoImportCommandHandler CommitHandler => new(Repo, Loader, Numbers, Clock);

    public async Task<BookeoImportPreview> PreviewFixtureAsync(string name = "bookeo_sample.xls")
    {
        var result = await PreviewHandler.Handle(
            new PreviewBookeoImportCommand(TenantId, name, Fixture(name), "dispatch@example.test"),
            CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : string.Empty);
        return result.Value;
    }

    public Task<BookeoImportPlan> PlanAsync(IReadOnlyList<BookeoParsedRow> rows) =>
        Loader.PlanAsync(rows, CancellationToken.None);

    /// <summary>The preview handler's persistence step for hand-built rows: a batch + its plan hash.</summary>
    public async Task<(BookeoImportBatch Batch, BookeoImportPlan Plan)> StageAsync(IReadOnlyList<BookeoParsedRow> rows)
    {
        var plan = await PlanAsync(rows);
        var batch = BookeoImportBatch.Create(
            TenantId, "staged.xls", "dispatch@example.test", Clock.GetUtcNow(),
            BookeoImportJson.SerializeRows(rows), plan.Hash, BookeoImportJson.SerializeSummary(plan.Summary));
        Repo.Batches.Add(batch);
        return (batch, plan);
    }

    public async Task<NorthernLink.Shared.Kernel.Result<BookeoImportCommitResult>> CommitAsync(Guid batchId, string planHash) =>
        await CommitHandler.Handle(
            new CommitBookeoImportCommand(TenantId, batchId, planHash, "dispatch@example.test"),
            CancellationToken.None);

    /// <summary>Preview + commit hand-built rows in one go; asserts the commit succeeded.</summary>
    public async Task<BookeoImportCommitResult> ImportAsync(IReadOnlyList<BookeoParsedRow> rows)
    {
        var (batch, plan) = await StageAsync(rows);
        var result = await CommitAsync(batch.Id, plan.Hash);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : string.Empty);
        return result.Value;
    }

    public TripManifest ManifestOf(Trip trip) =>
        Repo.Manifests.Single(m => m.Id == trip.ManifestId || m.TripNumber == trip.TripNumber);

    /// <summary>A dispatcher-made Scheduled Community trip on the route (driver + vehicle, as Trip.Schedule requires).</summary>
    public Trip AddExistingTrip(
        DateOnly date,
        TimeOnly start,
        TripDirection? direction = TripDirection.Outbound,
        TimeOnly? end = null,
        Guid? driverId = null,
        Guid? vehicleId = null,
        string tripNumber = "TR-0100",
        TripServiceType serviceType = TripServiceType.Community)
    {
        var stops = BookeoImportPlanner.OrientedStops(Route, direction);
        var trip = Trip.Schedule(
            TenantId, tripNumber, date, start, end, serviceType, Route.Id, Route.Name,
            stops[0].Name, stops[^1].Name, stops, Route.DistanceKm,
            scheduleTemplateId: null, roundTripKey: null, direction, isEmptyLeg: false,
            clientId: null, clientName: null, poNumber: null,
            driverId ?? TestPlanning.DriverId, TestPlanning.DriverName,
            vehicleId ?? Guid.Parse("00000000-0000-0000-0000-0000000005e9"), "NL-09",
            seatsCapacity: 14, seatsMinimum: null).Value;
        Repo.Trips.Add(trip);
        return trip;
    }
}

/// <summary>In-memory <see cref="IBookeoImportRepository"/>; <see cref="SaveAsync"/> just counts.</summary>
internal sealed class FakeBookeoImportRepository : IBookeoImportRepository
{
    public List<BookeoImportBatch> Batches { get; } = [];
    public List<BookeoBooking> Bookings { get; } = [];
    public List<BookeoProductMapping> ProductMappings { get; } = [];
    public List<BookeoUnitMapping> UnitMappings { get; } = [];
    public List<VehicleLookup> Vehicles { get; } = [];
    public List<Route> Routes { get; } = [];
    public List<Trip> Trips { get; } = [];
    public List<TripManifest> Manifests { get; } = [];
    public int SaveCount { get; private set; }
    public BookeoSaveOutcome NextSaveOutcome { get; set; } = BookeoSaveOutcome.Saved;

    public void AddBatch(BookeoImportBatch batch) => Batches.Add(batch);

    public Task<BookeoImportBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Batches.FirstOrDefault(b => b.Id == batchId));

    public Task<IReadOnlyList<BookeoImportBatch>> GetRecentBatchesAsync(int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BookeoImportBatch>>(Batches.OrderByDescending(b => b.UploadedAtUtc).Take(take).ToList());

    public Task<IReadOnlyList<BookeoBooking>> GetBookingsAsync(
        IReadOnlyCollection<string> bookingNumbers, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BookeoBooking>>(Bookings.Where(b => bookingNumbers.Contains(b.BookingNumber)).ToList());

    public void AddBooking(BookeoBooking booking) => Bookings.Add(booking);

    public Task<IReadOnlyList<BookeoProductMapping>> GetProductMappingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BookeoProductMapping>>(ProductMappings.ToList());

    public void AddProductMapping(BookeoProductMapping mapping) => ProductMappings.Add(mapping);

    public void RemoveProductMapping(BookeoProductMapping mapping) => ProductMappings.Remove(mapping);

    public Task<IReadOnlyList<BookeoUnitMapping>> GetUnitMappingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BookeoUnitMapping>>(UnitMappings.ToList());

    public void AddUnitMapping(BookeoUnitMapping mapping) => UnitMappings.Add(mapping);

    public void RemoveUnitMapping(BookeoUnitMapping mapping) => UnitMappings.Remove(mapping);

    public Task<IReadOnlyList<VehicleLookup>> GetVehiclesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<VehicleLookup>>(Vehicles.ToList());

    public Task<IReadOnlyList<Route>> GetRoutesAsync(IReadOnlyCollection<Guid> routeIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Route>>(Routes.Where(r => routeIds.Contains(r.Id)).ToList());

    public Task<IReadOnlyList<Trip>> GetTripsAsync(
        IReadOnlyCollection<DateOnly> serviceDates, IReadOnlyCollection<Guid> tripIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Trip>>(Trips.Where(t => serviceDates.Contains(t.ServiceDate) || tripIds.Contains(t.Id)).ToList());

    public Task<IReadOnlyDictionary<Guid, TripManifest>> GetManifestsForTripsAsync(
        IReadOnlyCollection<Trip> trips, CancellationToken cancellationToken = default)
    {
        var byTrip = new Dictionary<Guid, TripManifest>();
        foreach (var trip in trips)
        {
            var manifest = trip.ManifestId is { } id
                ? Manifests.FirstOrDefault(m => m.Id == id)
                : Manifests.FirstOrDefault(m => m.TripNumber == trip.TripNumber);
            if (manifest is not null)
            {
                byTrip[trip.Id] = manifest;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<Guid, TripManifest>>(byTrip);
    }

    public void AddTrip(Trip trip) => Trips.Add(trip);

    public void AddManifest(TripManifest manifest) => Manifests.Add(manifest);

    public Task<BookeoSaveOutcome> SaveAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        var outcome = NextSaveOutcome;
        NextSaveOutcome = BookeoSaveOutcome.Saved;
        return Task.FromResult(outcome);
    }
}
