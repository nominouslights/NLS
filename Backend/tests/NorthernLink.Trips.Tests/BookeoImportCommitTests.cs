using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.BookeoImports;
using NorthernLink.Trips.Application.BookeoImports.Commit;
using NorthernLink.Trips.Application.BookeoImports.Preview;
using NorthernLink.Trips.Application.Manifests.Update;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// Preview → commit end to end over the in-memory fakes: what a commit writes, that it writes it
/// once, that it refuses a stale preview, and that it only ever touches its own manifest rows and
/// its own trips. (The Postgres round trip — unique index, RLS, the raw jsonb — is in
/// NorthernLink.Trips.IntegrationTests.)
/// </summary>
public class BookeoImportCommitTests
{
    private static readonly DateOnly Oct5 = new(2026, 10, 5);
    private static readonly DateOnly Oct6 = new(2026, 10, 6);

    [Fact]
    public async Task Committing_the_fixture_creates_three_community_trips_with_their_manifests_and_the_ledger()
    {
        var bed = new BookeoTestBed();
        var preview = await bed.PreviewFixtureAsync();

        var result = await bed.CommitAsync(preview.BatchId, preview.PlanHash);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : string.Empty);
        Assert.Equal(new BookeoImportCommitResult(
            preview.BatchId, TripsCreated: 3, TripsUpdated: 0, TripsCancelled: 0,
            BookingsImported: 4, BookingsCancelled: 0, GroupsSkippedBlocked: 0,
            CreatedTripNumbers: result.Value.CreatedTripNumbers), result.Value);
        Assert.Equal(3, result.Value.CreatedTripNumbers.Count);

        Assert.Equal(3, bed.Repo.Trips.Count);
        Assert.All(bed.Repo.Trips, t =>
        {
            Assert.Equal(TripServiceType.Community, t.ServiceType);
            Assert.Equal(TripStatus.Scheduled, t.Status);
            Assert.True(t.IsCreatedByBookeoImport);
            Assert.Null(t.DriverId);
            Assert.NotNull(t.ManifestId);
        });

        var shuttle = bed.Repo.Trips.Single(t => t.ServiceDate == Oct5);
        Assert.Equal(new TimeOnly(8, 0), shuttle.WindowStart);
        Assert.Equal(new TimeOnly(12, 30), shuttle.WindowEnd);
        Assert.Equal(TripDirection.Outbound, shuttle.Direction);
        Assert.Equal(bed.Route.Id, shuttle.RouteId);
        Assert.Equal("Thompson", shuttle.Origin);
        Assert.Equal("Lynn Lake", shuttle.Destination);
        Assert.Equal(3, shuttle.SeatsConfirmed);
        Assert.Equal(BookeoTestBed.VanId, shuttle.VehicleId);
        Assert.Equal(14, shuttle.SeatsCapacity);

        var toThompson = bed.Repo.Trips.Single(t => t.ServiceDate == Oct6 && t.WindowStart == new TimeOnly(13, 30));
        Assert.Equal(TripDirection.Inbound, toThompson.Direction);
        Assert.Equal("Lynn Lake", toThompson.Origin);
        Assert.Null(toThompson.VehicleId);

        var manifest = bed.ManifestOf(shuttle);
        Assert.Equal(ManifestSource.Dispatcher, manifest.Source);
        Assert.Equal("Bookeo import · dispatch@example.test", manifest.EnteredBy);
        Assert.Equal(3, manifest.Passengers.Count);
        Assert.Equal(240m, manifest.FaresCollectedCad);

        Assert.Equal(4, bed.Repo.Bookings.Count);
        Assert.DoesNotContain(bed.Repo.Bookings, b => b.BookingNumber.EndsWith("005", StringComparison.Ordinal));
        Assert.Equal(shuttle.Id, bed.Repo.Bookings.Single(b => b.BookingNumber.EndsWith("002", StringComparison.Ordinal)).TripId);
        Assert.True(bed.Repo.Batches.Single().IsCommitted);
    }

    [Fact]
    public async Task Committing_twice_is_AlreadyCommitted_and_a_re_preview_is_all_unchanged()
    {
        var bed = new BookeoTestBed();
        var preview = await bed.PreviewFixtureAsync();
        Assert.True((await bed.CommitAsync(preview.BatchId, preview.PlanHash)).IsSuccess);

        var again = await bed.CommitAsync(preview.BatchId, preview.PlanHash);

        Assert.True(again.IsFailure);
        Assert.Equal("Trips.BookeoImport.AlreadyCommitted", again.Error.Code);
        Assert.Equal(3, bed.Repo.Trips.Count);

        var rePreview = await bed.PreviewFixtureAsync();
        Assert.Equal(4, rePreview.Summary.Unchanged);
        Assert.Equal(1, rePreview.Summary.Skipped);
        Assert.Equal(0, rePreview.Summary.New);
        Assert.Equal(0, rePreview.Summary.TripsToCreate);
        Assert.Equal(0, rePreview.Summary.TripsToUpdate);
        Assert.All(rePreview.Groups, g => Assert.Equal("Unchanged", g.Action));
        Assert.All(rePreview.Groups, g => Assert.NotNull(g.ExistingTripId));

        var reCommit = await bed.CommitAsync(rePreview.BatchId, rePreview.PlanHash);
        Assert.True(reCommit.IsSuccess);
        Assert.Equal(0, reCommit.Value.TripsCreated);
        Assert.Equal(3, bed.Repo.Trips.Count);
        Assert.Equal(4, bed.Repo.Bookings.Count);
    }

    [Fact]
    public async Task A_concurrent_confirm_that_loses_the_batch_race_is_AlreadyCommitted()
    {
        var bed = new BookeoTestBed();
        var preview = await bed.PreviewFixtureAsync();
        bed.Repo.NextSaveOutcome = BookeoSaveOutcome.BatchAlreadyCommitted;

        var result = await bed.CommitAsync(preview.BatchId, preview.PlanHash);

        Assert.Equal("Trips.BookeoImport.AlreadyCommitted", result.Error.Code);
    }

    [Fact]
    public async Task A_preview_the_database_has_moved_past_is_PreviewStale()
    {
        var bed = new BookeoTestBed();
        var preview = await bed.PreviewFixtureAsync();

        // A dispatcher creates the 08:00 run by hand between preview and confirm: the plan now
        // updates that trip instead of creating one.
        bed.AddExistingTrip(Oct5, new TimeOnly(8, 0), TripDirection.Outbound);
        var result = await bed.CommitAsync(preview.BatchId, preview.PlanHash);

        Assert.True(result.IsFailure);
        Assert.Equal("Trips.BookeoImport.PreviewStale", result.Error.Code);
        Assert.Single(bed.Repo.Trips); // Nothing applied.
        Assert.Empty(bed.Repo.Bookings);
        Assert.False(bed.Repo.Batches.Single().IsCommitted);
    }

    [Fact]
    public async Task A_save_conflict_is_PreviewStale_and_unknown_batches_are_not_found()
    {
        var bed = new BookeoTestBed();
        var preview = await bed.PreviewFixtureAsync();
        bed.Repo.NextSaveOutcome = BookeoSaveOutcome.Conflict;

        Assert.Equal("Trips.BookeoImport.PreviewStale", (await bed.CommitAsync(preview.BatchId, preview.PlanHash)).Error.Code);
        Assert.Equal("Trips.BookeoImport.BatchNotFound", (await bed.CommitAsync(Guid.NewGuid(), preview.PlanHash)).Error.Code);
    }

    [Fact]
    public async Task Manually_entered_passengers_are_never_touched()
    {
        var bed = new BookeoTestBed();
        var trip = bed.AddExistingTrip(Oct5, new TimeOnly(8, 0), TripDirection.Outbound);
        var manual = new ManifestPassenger { Name = "Walk-up Rider", Phone = "2045550000", IdVerified = true };
        var manifest = TripManifest.Create(
            BookeoTestBed.TenantId, Oct5, trip.TripNumber, trip.RouteName, trip.Direction, null,
            [manual], allSeatbeltsVerified: true, cargo: [], allCargoSecured: null, ManifestSource.Dispatcher, "dispatcher").Value;
        bed.Repo.Manifests.Add(manifest);

        await bed.ImportAsync(BookeoTestBed.FixtureRows());
        Assert.Equal(4, manifest.Passengers.Count);
        Assert.Equal(manual, manifest.Passengers[0]);

        // Both of its bookings cancelled: the import takes its own rows off and nothing else.
        await bed.ImportAsync(BookeoTestBed.FixtureRows()
            .Where(r => r.ServiceDate == Oct5)
            .Select(r => r with { Status = "canceled", IsCanceled = true })
            .ToList());

        Assert.Equal(manual, Assert.Single(manifest.Passengers));
        Assert.True(manifest.AllSeatbeltsVerified);
    }

    [Fact]
    public async Task An_import_created_trip_left_with_no_passengers_is_cancelled()
    {
        var bed = new BookeoTestBed();
        await bed.ImportAsync(BookeoTestBed.FixtureRows());
        var trip = bed.Repo.Trips.Single(t => t.ServiceDate == Oct6 && t.WindowStart == new TimeOnly(13, 30));

        var result = await bed.ImportAsync([BookeoTestBed.FixtureRow("003") with { Status = "canceled", IsCanceled = true }]);

        Assert.Equal(1, result.TripsCancelled);
        Assert.Equal(1, result.BookingsCancelled);
        Assert.Equal(TripStatus.Cancelled, trip.Status);
        Assert.Empty(bed.ManifestOf(trip).Passengers);
        var ledger = bed.Repo.Bookings.Single(b => b.BookingNumber.EndsWith("003", StringComparison.Ordinal));
        Assert.Null(ledger.TripId);
        Assert.Equal("canceled", ledger.BookeoStatus);
    }

    [Fact]
    public async Task A_trip_the_import_did_not_create_is_never_cancelled_even_when_emptied()
    {
        var bed = new BookeoTestBed();
        var trip = bed.AddExistingTrip(Oct6, new TimeOnly(13, 30), TripDirection.Inbound);
        await bed.ImportAsync(BookeoTestBed.FixtureRows());
        Assert.Equal(trip.Id, bed.Repo.Bookings.Single(b => b.BookingNumber.EndsWith("003", StringComparison.Ordinal)).TripId);
        Assert.Single(bed.ManifestOf(trip).Passengers);

        var result = await bed.ImportAsync([BookeoTestBed.FixtureRow("003") with { Status = "canceled", IsCanceled = true }]);

        Assert.Equal(0, result.TripsCancelled);
        Assert.Equal(1, result.TripsUpdated);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
        Assert.Empty(bed.ManifestOf(trip).Passengers);
    }

    [Fact]
    public async Task A_blocked_group_is_skipped_and_its_bookings_are_not_written_to_the_ledger()
    {
        var bed = new BookeoTestBed();
        bed.Van.SeatingCapacity = 2; // The 08:00 group (3 passengers) cannot fit.
        var preview = await bed.PreviewFixtureAsync();

        var result = await bed.CommitAsync(preview.BatchId, preview.PlanHash);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.GroupsSkippedBlocked);
        Assert.Equal(2, result.Value.TripsCreated);
        Assert.DoesNotContain(bed.Repo.Bookings, b => b.BookingNumber.EndsWith("001", StringComparison.Ordinal));
        Assert.DoesNotContain(bed.Repo.Bookings, b => b.BookingNumber.EndsWith("002", StringComparison.Ordinal));

        // Fixed, the held-back bookings come back as New on the next upload.
        bed.Van.SeatingCapacity = 14;
        var again = await bed.PreviewFixtureAsync();
        Assert.Equal(2, again.Summary.New);
        Assert.Equal(1, again.Summary.TripsToCreate);
    }

    [Fact]
    public async Task Preview_rejects_missing_oversized_and_non_spreadsheet_files()
    {
        var bed = new BookeoTestBed();
        async Task<string> Code(string name, byte[] content) =>
            (await bed.PreviewHandler.Handle(
                new PreviewBookeoImportCommand(BookeoTestBed.TenantId, name, content, "x"), CancellationToken.None)).Error.Code;

        Assert.Equal("Trips.BookeoImport.FileRequired", await Code("a.xls", []));
        Assert.Equal("Trips.BookeoImport.FileTooLarge", await Code("a.xls", new byte[6 * 1024 * 1024]));
        Assert.Equal("Trips.BookeoImport.UnsupportedFile", await Code("a.csv", "x"u8.ToArray()));
        Assert.Equal("Trips.BookeoImport.UnsupportedFile", await Code("a.xls", "not a workbook"u8.ToArray()));
        Assert.Empty(bed.Repo.Batches);
    }

    [Fact]
    public void A_manual_manifest_edit_that_drops_the_ref_keeps_the_import_owning_its_rows()
    {
        ManifestPassenger[] existing =
        [
            new() { Name = "Alex Sample", ExternalRef = "bookeo:9000000000000001" },
            new() { Name = "Walk-up Rider" },
        ];
        ManifestPassenger[] incoming =
        [
            new() { Name = "Alex Sample", BoardedOn = true },
            new() { Name = "Walk-up Rider", BoardedOn = true },
        ];

        var carried = UpdateTripManifestCommandHandler.CarryExternalRefs(existing, incoming);

        Assert.Equal("bookeo:9000000000000001", carried[0].ExternalRef);
        Assert.True(carried[0].BoardedOn);
        Assert.Null(carried[1].ExternalRef);
    }
}
