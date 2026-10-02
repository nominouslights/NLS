using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.BookeoImports;
using NorthernLink.Trips.Application.BookeoImports.Commit;
using NorthernLink.Trips.Application.BookeoImports.Preview;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.BookeoImports;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Infrastructure.BookeoImports;
using NorthernLink.Trips.Infrastructure.Persistence;
using Xunit;

namespace NorthernLink.Trips.IntegrationTests;

/// <summary>
/// The Bookeo import against a real Postgres, as the non-superuser app role so RLS is enforced:
/// a preview → commit of the synthetic fixture lands in one transaction, a second commit is
/// refused, a re-preview is all Unchanged, the stored parsed_rows jsonb carries no tax key, the
/// new tables are tenant-isolated by RLS, and the DB-atomic guards (the batch's committed_at_utc
/// concurrency token, the unique unit-mapping index) surface as outcomes rather than exceptions.
/// Each test runs in its own tenant, so they never see each other's rows.
/// </summary>
[Collection("postgres")]
public class BookeoImportPersistenceTests(PostgresFixture fixture)
{
    private const string FromThompson = "41578WMAM3X1A0E69B7F59";
    private const string ToThompson = "41578HWK6CT1A0E4970BA2";
    private const string LakeToRapids = "41578CPNRWL1A0FA82032B";

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);
    }

    private static byte[] Fixture() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", "bookeo_sample.xls"));

    private async Task<Guid> SeedTenantAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var context = fixture.CreateTripsContext(tenantId);

        var route = Route.Create(
            tenantId,
            "Thompson ↔ Lynn Lake",
            [
                new RouteStop { Name = "Thompson", Order = 0 },
                new RouteStop { Name = "Leaf Rapids", Order = 1 },
                new RouteStop { Name = "Lynn Lake", Order = 2 },
            ],
            distanceKm: 320,
            estimatedDuration: TimeSpan.FromMinutes(270),
            requiredLicenceClass: null).Value;
        context.Routes.Add(route);

        var vanId = Guid.NewGuid();
        context.VehicleLookups.Add(new VehicleLookup
        {
            VehicleId = vanId,
            TenantId = tenantId,
            UnitNumber = "NL-02",
            Status = VehicleLookup.ActiveStatus,
            RequiredLicenceClass = "Class 4",
            SeatingCapacity = 14,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });

        context.BookeoProductMappings.Add(BookeoProductMapping.Create(
            tenantId, FromThompson, "Shuttle from Thompson", null, route.Id, TripDirection.Outbound, ResidentStopRole.Dropoff).Value);
        context.BookeoProductMappings.Add(BookeoProductMapping.Create(
            tenantId, ToThompson, "Shuttle to Thompson", null, route.Id, TripDirection.Inbound, ResidentStopRole.Pickup).Value);
        context.BookeoProductMappings.Add(BookeoProductMapping.Create(
            tenantId, LakeToRapids, "Lynn Lake <-> Leaf Rapids", "Leaf Rapids to Lynn Lake", route.Id, TripDirection.Outbound, ResidentStopRole.Pickup).Value);
        context.BookeoUnitMappings.Add(BookeoUnitMapping.Create(tenantId, "Ford Transit 150", vanId).Value);

        await context.SaveChangesAsync();
        return tenantId;
    }

    private static async Task<BookeoImportPreview> PreviewAsync(TripsDbContext context, Guid tenantId)
    {
        var repository = new BookeoImportRepository(context);
        var handler = new PreviewBookeoImportCommandHandler(
            new ExcelBookeoWorkbookReader(), new BookeoImportPlanLoader(repository, new FixedClock()), repository, new FixedClock());

        var result = await handler.Handle(
            new PreviewBookeoImportCommand(tenantId, "bookeo_sample.xls", Fixture(), "dispatch@example.test"),
            CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : string.Empty);
        return result.Value;
    }

    private static Task<NorthernLink.Shared.Kernel.Result<BookeoImportCommitResult>> CommitAsync(
        TripsDbContext context, Guid tenantId, BookeoImportPreview preview)
    {
        var repository = new BookeoImportRepository(context);
        var handler = new CommitBookeoImportCommandHandler(
            repository, new BookeoImportPlanLoader(repository, new FixedClock()), new TripNumberGenerator(context), new FixedClock());
        return handler.Handle(
            new CommitBookeoImportCommand(tenantId, preview.BatchId, preview.PlanHash, "dispatch@example.test"),
            CancellationToken.None);
    }

    [Fact]
    public async Task Preview_then_commit_writes_trips_manifests_and_ledger_once_and_a_re_preview_is_unchanged()
    {
        var tenantId = await SeedTenantAsync();

        BookeoImportPreview preview;
        await using (var context = fixture.CreateTripsContext(tenantId))
        {
            preview = await PreviewAsync(context, tenantId);
        }

        Assert.Equal(3, preview.Summary.TripsToCreate);

        await using (var context = fixture.CreateTripsContext(tenantId))
        {
            var committed = await CommitAsync(context, tenantId, preview);
            Assert.True(committed.IsSuccess, committed.IsFailure ? committed.Error.Code : string.Empty);
            Assert.Equal(3, committed.Value.TripsCreated);
            Assert.Equal(4, committed.Value.BookingsImported);
        }

        await using (var context = fixture.CreateTripsContext(tenantId))
        {
            var trips = await context.Trips.AsNoTracking().ToListAsync();
            Assert.Equal(3, trips.Count);
            Assert.All(trips, t => Assert.Equal(Trip.BookeoImportSource, t.ImportSource));

            var manifests = await context.Manifests.AsNoTracking().ToListAsync();
            Assert.Equal(5, manifests.Sum(m => m.Passengers.Count));
            Assert.All(manifests.SelectMany(m => m.Passengers), p => Assert.StartsWith("bookeo:", p.ExternalRef, StringComparison.Ordinal));

            Assert.Equal(4, await context.BookeoBookings.CountAsync());
            var ledger = await context.BookeoBookings.AsNoTracking().SingleAsync(b => b.BookingNumber == "9000000000000001");
            Assert.Equal(2, ledger.Passengers.Count);
            Assert.Null(ledger.Passengers[1].Email); // "Bo Sample" had no email in Bookeo.

            var again = await CommitAsync(context, tenantId, preview);
            Assert.Equal("Trips.BookeoImport.AlreadyCommitted", again.Error.Code);
        }

        await using (var context = fixture.CreateTripsContext(tenantId))
        {
            var rePreview = await PreviewAsync(context, tenantId);
            Assert.Equal(4, rePreview.Summary.Unchanged);
            Assert.Equal(1, rePreview.Summary.Skipped);
            Assert.Equal(0, rePreview.Summary.TripsToCreate);
            Assert.All(rePreview.Groups, g => Assert.Equal("Unchanged", g.Action));
            Assert.Equal(3, await context.Trips.CountAsync());
        }
    }

    [Fact]
    public async Task The_stored_parsed_rows_jsonb_has_no_tax_key()
    {
        var tenantId = await SeedTenantAsync();
        await using var context = fixture.CreateTripsContext(tenantId);
        var preview = await PreviewAsync(context, tenantId);

        var raw = await context.Database
            .SqlQuery<string>($"SELECT parsed_rows::text AS \"Value\" FROM trips.bookeo_import_batches WHERE id = {preview.BatchId}")
            .SingleAsync();

        var keys = new List<string>();
        void Collect(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    keys.Add(property.Name);
                    Collect(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item);
                }
            }
        }

        Collect(JsonDocument.Parse(raw).RootElement);
        Assert.Contains("totalGrossCad", keys);
        Assert.DoesNotContain(keys, k => k.Contains("gst", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(keys, k => k.Contains("net", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("11.43", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_import_tables_are_isolated_by_row_level_security()
    {
        var tenantId = await SeedTenantAsync();
        await using (var context = fixture.CreateTripsContext(tenantId))
        {
            await PreviewAsync(context, tenantId);
        }

        // Another tenant, reading with the API-level filter switched OFF: RLS alone must hide the rows.
        await using var other = fixture.CreateTripsContext(Guid.NewGuid());
        Assert.Equal(0, await other.BookeoImportBatches.IgnoreQueryFilters().CountAsync(b => b.TenantId == tenantId));
        Assert.Equal(0, await other.BookeoProductMappings.IgnoreQueryFilters().CountAsync(m => m.TenantId == tenantId));
        Assert.Equal(0, await other.BookeoUnitMappings.IgnoreQueryFilters().CountAsync(m => m.TenantId == tenantId));
        Assert.Equal(0, await other.BookeoBookings.IgnoreQueryFilters().CountAsync(b => b.TenantId == tenantId));
    }

    [Fact]
    public async Task Two_confirms_of_one_batch_race_on_committed_at_and_only_one_wins()
    {
        var tenantId = await SeedTenantAsync();
        Guid batchId;
        await using (var context = fixture.CreateTripsContext(tenantId))
        {
            batchId = (await PreviewAsync(context, tenantId)).BatchId;
        }

        await using var first = fixture.CreateTripsContext(tenantId);
        await using var second = fixture.CreateTripsContext(tenantId);
        var firstBatch = await new BookeoImportRepository(first).GetBatchAsync(batchId);
        var secondBatch = await new BookeoImportRepository(second).GetBatchAsync(batchId);
        firstBatch!.MarkCommitted("a", DateTimeOffset.UtcNow);
        secondBatch!.MarkCommitted("b", DateTimeOffset.UtcNow);

        Assert.Equal(BookeoSaveOutcome.Saved, await new BookeoImportRepository(first).SaveAsync());
        Assert.Equal(BookeoSaveOutcome.BatchAlreadyCommitted, await new BookeoImportRepository(second).SaveAsync());
    }

    [Fact]
    public async Task A_duplicate_unit_mapping_trips_the_unique_index_as_a_conflict()
    {
        var tenantId = await SeedTenantAsync();
        await using var context = fixture.CreateTripsContext(tenantId);
        var repository = new BookeoImportRepository(context);
        var vehicleId = (await repository.GetVehiclesAsync()).Single().VehicleId;

        repository.AddUnitMapping(BookeoUnitMapping.Create(tenantId, "ford transit   150", vehicleId).Value);

        Assert.Equal(BookeoSaveOutcome.Conflict, await repository.SaveAsync());
    }

    [Fact]
    public async Task Product_mappings_are_unique_per_code_and_destination_case_insensitively()
    {
        var tenantId = await SeedTenantAsync();
        await using var context = fixture.CreateTripsContext(tenantId);
        var repository = new BookeoImportRepository(context);
        var routeId = (await repository.GetProductMappingsAsync()).First().RouteId;

        repository.AddProductMapping(BookeoProductMapping.Create(
            tenantId, LakeToRapids, "x", "LEAF RAPIDS TO LYNN LAKE", routeId, null, ResidentStopRole.Pickup).Value);

        Assert.Equal(BookeoSaveOutcome.Conflict, await repository.SaveAsync());
    }
}
