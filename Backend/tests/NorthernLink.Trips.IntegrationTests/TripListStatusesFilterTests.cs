using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Infrastructure.Persistence;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;
using Xunit;

namespace NorthernLink.Trips.IntegrationTests;

/// <summary>
/// <c>TripFilter.Statuses</c> against real Postgres: a status SET matched as one IN in the
/// same statement as the date, client and tenant predicates. This is the read the accruals
/// report makes — every trip of one client dated up to a cutoff, no lower bound, whose status
/// is still open (Scheduled, InProgress, ReadyForBilling, Invoiced) — and it exists so that
/// read is one snapshot instead of four per-status calls a status change can slip between.
/// <para>
/// Seeds <c>rm_trips</c> directly (as <see cref="TripListPagingTests"/> does), under a client
/// id no other test uses, so the <c>to</c>-only bound cannot sweep in anyone else's rows.
/// </para>
/// </summary>
[Collection("postgres")]
public class TripListStatusesFilterTests(PostgresFixture fixture)
{
    private static readonly Guid TenantB = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid Client = Guid.Parse("5a7e0000-0000-0000-0000-00000000acc1");
    private static readonly Guid OtherClient = Guid.Parse("5a7e0000-0000-0000-0000-00000000acc2");
    private static readonly DateOnly Cutoff = new(2032, 4, 30);

    private static readonly TripStatus[] Outstanding =
        [TripStatus.Scheduled, TripStatus.InProgress, TripStatus.ReadyForBilling, TripStatus.Invoiced];

    [Fact]
    public async Task One_call_returns_every_listed_status_up_to_the_cutoff_for_the_client()
    {
        await SeedAsync();
        var service = CreateService(PostgresFixture.TenantA);

        var (items, totalCount) = await service.GetTripsAsync(
            new TripFilter(To: Cutoff, ClientId: Client, Statuses: Outstanding));

        // One row per outstanding status, from months apart (no lower bound) — and nothing that is
        // Completed, Cancelled or WrittenOff, after the cutoff, another client's, or another tenant's.
        Assert.Equal(
            ["ST-2032-01", "ST-2032-02", "ST-2032-03", "ST-2032-04"],
            items.Select(t => t.TripNumber));
        Assert.Equal(
            Outstanding.Select(s => s.ToString()).Order(),
            items.Select(t => t.Status).Order());
        Assert.All(items, t => Assert.Equal(Client, t.ClientId));
        Assert.All(items, t => Assert.True(t.ServiceDate <= Cutoff));
        Assert.Equal(items.Count, totalCount);
    }

    [Fact]
    public async Task A_one_member_set_matches_exactly_what_the_single_status_filter_does()
    {
        await SeedAsync();
        var service = CreateService(PostgresFixture.TenantA);

        var (viaSet, setTotal) = await service.GetTripsAsync(
            new TripFilter(To: Cutoff, ClientId: Client, Statuses: [TripStatus.Invoiced]));
        var (viaSingle, singleTotal) = await service.GetTripsAsync(
            new TripFilter(To: Cutoff, ClientId: Client, Status: TripStatus.Invoiced));

        Assert.Equal(viaSingle.Select(t => t.Id), viaSet.Select(t => t.Id));
        Assert.Equal(singleTotal, setTotal);
        Assert.Equal(nameof(TripStatus.Invoiced), Assert.Single(viaSet).Status);
    }

    [Fact]
    public async Task Paging_composes_with_the_set_and_the_total_describes_the_whole_match()
    {
        await SeedAsync();
        var service = CreateService(PostgresFixture.TenantA);

        var (all, _) = await service.GetTripsAsync(
            new TripFilter(To: Cutoff, ClientId: Client, Statuses: Outstanding));
        var (page, total) = await service.GetTripsAsync(
            new TripFilter(To: Cutoff, ClientId: Client, Statuses: Outstanding, Page: 2, PageSize: 3));

        Assert.Equal(all.Count, total);
        Assert.Equal(all.Skip(3).Select(t => t.Id), page.Select(t => t.Id));
    }

    [Fact]
    public async Task An_empty_set_matches_nothing_rather_than_everything()
    {
        // The endpoint rejects ?statuses= before it gets here; this pins what a direct caller of
        // the read service gets, so an empty set can never quietly mean "no filter".
        await SeedAsync();
        var service = CreateService(PostgresFixture.TenantA);

        var (items, totalCount) = await service.GetTripsAsync(
            new TripFilter(To: Cutoff, ClientId: Client, Statuses: []));

        Assert.Empty(items);
        Assert.Equal(0, totalCount);
    }

    private ITripReadService CreateService(Guid tenantId) =>
        new TripReadService(fixture.CreateTripsContext(tenantId));

    private async Task SeedAsync()
    {
        await using (var tenantA = fixture.CreateTripsContext(PostgresFixture.TenantA))
        {
            if (!await tenantA.TripReadModels.AnyAsync(t => t.ClientId == Client))
            {
                tenantA.TripReadModels.AddRange(
                    // Months apart on purpose: the report has no lower bound.
                    Row(PostgresFixture.TenantA, "ST-2032-01", new DateOnly(2031, 11, 3), TripStatus.Invoiced, Client),
                    Row(PostgresFixture.TenantA, "ST-2032-02", new DateOnly(2032, 2, 14), TripStatus.ReadyForBilling, Client),
                    Row(PostgresFixture.TenantA, "ST-2032-03", new DateOnly(2032, 4, 29), TripStatus.InProgress, Client),
                    // On the cutoff — the bound is inclusive.
                    Row(PostgresFixture.TenantA, "ST-2032-04", Cutoff, TripStatus.Scheduled, Client),
                    // Settled or dead statuses — must not appear.
                    Row(PostgresFixture.TenantA, "ST-2032-05", new DateOnly(2032, 3, 1), TripStatus.Completed, Client),
                    Row(PostgresFixture.TenantA, "ST-2032-06", new DateOnly(2032, 3, 2), TripStatus.Cancelled, Client),
                    Row(PostgresFixture.TenantA, "ST-2032-07", new DateOnly(2032, 3, 3), TripStatus.WrittenOff, Client),
                    // After the cutoff — must not appear.
                    Row(PostgresFixture.TenantA, "ST-2032-08", new DateOnly(2032, 5, 1), TripStatus.Scheduled, Client),
                    // Another client — must not appear.
                    Row(PostgresFixture.TenantA, "ST-2032-09", new DateOnly(2032, 4, 1), TripStatus.Scheduled, OtherClient));
                await tenantA.SaveChangesAsync();
            }
        }

        // Same client id, same status, inside the window — but another tenant's row.
        await using var tenantB = fixture.CreateTripsContext(TenantB);
        if (!await tenantB.TripReadModels.AnyAsync(t => t.ClientId == Client))
        {
            tenantB.TripReadModels.Add(
                Row(TenantB, "ST-2032-10", new DateOnly(2032, 4, 1), TripStatus.Scheduled, Client));
            await tenantB.SaveChangesAsync();
        }
    }

    private static TripReadModel Row(Guid tenantId, string number, DateOnly date, TripStatus status, Guid clientId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        TripNumber = number,
        ServiceDate = date,
        WindowStart = new TimeOnly(8, 0),
        WindowEnd = null,
        ServiceType = nameof(TripServiceType.ContractCrew),
        RouteId = null,
        RouteName = "Thompson – Lynn Lake",
        Origin = "Thompson",
        Destination = "Lynn Lake",
        Stops = [],
        DistanceKm = 320,
        ScheduleTemplateId = null,
        RoundTripKey = null,
        Direction = null,
        IsEmptyLeg = false,
        ClientId = clientId,
        ClientName = "Accruals Test Client",
        PoNumber = null,
        DriverId = null,
        DriverName = null,
        VehicleId = null,
        VehicleUnit = null,
        SeatsCapacity = null,
        SeatsConfirmed = 0,
        SeatsMinimum = null,
        DemandGuaranteed = false,
        Status = status.ToString(),
        ManifestId = null,
        HasPostTripInspection = false,
        CompletedAtUtc = null,
        CancelledReason = status == TripStatus.Cancelled ? "Weather" : null,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
        Version = 1,
    };
}
