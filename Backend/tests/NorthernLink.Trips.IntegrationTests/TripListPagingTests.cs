using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Infrastructure.Persistence;
using NorthernLink.Trips.Infrastructure.Persistence.ReadModels;
using Xunit;

namespace NorthernLink.Trips.IntegrationTests;

/// <summary>
/// The trips list is server-paged, so the dispatcher's month/quarter browsing never pulls a
/// whole period into the browser. These pin the properties the UI depends on and that a unit
/// test over a fake could not: real Postgres ordering, a total that describes the FILTERED
/// set rather than the page, and pages that partition the set without overlap or omission.
/// <para>
/// Seeds <c>rm_trips</c> directly — the read service reads the projection, and going through
/// the aggregate would make these tests about the projector instead of about paging.
/// </para>
/// </summary>
[Collection("postgres")]
public class TripListPagingTests(PostgresFixture fixture)
{
    private static readonly DateOnly PeriodStart = new(2031, 3, 1);
    private static readonly DateOnly PeriodEnd = new(2031, 3, 31);

    /// <summary>Seven trips over three days in March 2031, well clear of any other test's data.</summary>
    private const int SeedCount = 7;

    [Fact]
    public async Task Unpaged_returns_every_match_and_totals_them()
    {
        await SeedPeriodAsync();
        var service = CreateService();

        var (items, totalCount) = await service.GetTripsAsync(new TripFilter(From: PeriodStart, To: PeriodEnd));

        Assert.Equal(SeedCount, items.Count);
        Assert.Equal(SeedCount, totalCount);
    }

    [Fact]
    public async Task Pages_partition_the_set_in_service_date_then_window_then_number_order()
    {
        await SeedPeriodAsync();
        var service = CreateService();

        var (pageOne, totalOne) = await service.GetTripsAsync(
            new TripFilter(From: PeriodStart, To: PeriodEnd, Page: 1, PageSize: 3));
        var (pageTwo, totalTwo) = await service.GetTripsAsync(
            new TripFilter(From: PeriodStart, To: PeriodEnd, Page: 2, PageSize: 3));
        var (pageThree, _) = await service.GetTripsAsync(
            new TripFilter(From: PeriodStart, To: PeriodEnd, Page: 3, PageSize: 3));

        Assert.Equal(3, pageOne.Count);
        Assert.Equal(3, pageTwo.Count);
        Assert.Single(pageThree);

        // The total describes the filtered set, not the page — this is what the
        // "1–50 of 312" footer reads.
        Assert.Equal(SeedCount, totalOne);
        Assert.Equal(SeedCount, totalTwo);

        var paged = pageOne.Concat(pageTwo).Concat(pageThree).ToList();
        Assert.Equal(SeedCount, paged.Select(t => t.Id).Distinct().Count()); // no overlap

        var (all, _) = await service.GetTripsAsync(new TripFilter(From: PeriodStart, To: PeriodEnd));
        Assert.Equal(all.Select(t => t.Id), paged.Select(t => t.Id)); // same set, same order

        var ordering = paged
            .Select(t => (t.ServiceDate, t.WindowStart, t.TripNumber))
            .ToList();
        Assert.Equal(ordering.OrderBy(k => k.ServiceDate).ThenBy(k => k.WindowStart).ThenBy(k => k.TripNumber), ordering);
    }

    [Fact]
    public async Task A_page_past_the_end_is_empty_but_still_reports_the_true_total()
    {
        await SeedPeriodAsync();
        var service = CreateService();

        var (items, totalCount) = await service.GetTripsAsync(
            new TripFilter(From: PeriodStart, To: PeriodEnd, Page: 99, PageSize: 50));

        Assert.Empty(items);
        // The UI uses this to fall back to the last page that still has rows.
        Assert.Equal(SeedCount, totalCount);
    }

    [Fact]
    public async Task ExcludeCancelled_narrows_the_total_as_well_as_the_page()
    {
        await SeedPeriodAsync();
        var service = CreateService();

        var (items, totalCount) = await service.GetTripsAsync(
            new TripFilter(From: PeriodStart, To: PeriodEnd, ExcludeCancelled: true));

        Assert.DoesNotContain(items, t => t.Status == nameof(TripStatus.Cancelled));
        Assert.Equal(items.Count, totalCount);
        Assert.True(totalCount < SeedCount, "the seed includes a cancelled trip");
    }

    [Fact]
    public async Task AssignedOnly_and_OpenOnly_are_complements_within_the_scheduled_set()
    {
        await SeedPeriodAsync();
        var service = CreateService();

        var (assigned, assignedTotal) = await service.GetTripsAsync(
            new TripFilter(From: PeriodStart, To: PeriodEnd, AssignedOnly: true));
        var (open, openTotal) = await service.GetTripsAsync(
            new TripFilter(From: PeriodStart, To: PeriodEnd, OpenOnly: true));

        Assert.All(assigned, t => Assert.NotNull(t.DriverId));
        Assert.Equal(assigned.Count, assignedTotal);

        Assert.All(open, t => Assert.Null(t.DriverId));
        Assert.All(open, t => Assert.Equal(nameof(TripStatus.Scheduled), t.Status));
        Assert.Equal(open.Count, openTotal);

        Assert.Empty(assigned.Select(t => t.Id).Intersect(open.Select(t => t.Id)));
    }

    [Fact]
    public async Task Paging_composes_with_a_filter_rather_than_paging_the_unfiltered_set()
    {
        await SeedPeriodAsync();
        var service = CreateService();

        var (all, allTotal) = await service.GetTripsAsync(
            new TripFilter(From: PeriodStart, To: PeriodEnd, ExcludeCancelled: true));
        var (firstPage, pagedTotal) = await service.GetTripsAsync(
            new TripFilter(From: PeriodStart, To: PeriodEnd, ExcludeCancelled: true, Page: 1, PageSize: 2));

        Assert.Equal(allTotal, pagedTotal);
        Assert.Equal(2, firstPage.Count);
        Assert.Equal(all.Take(2).Select(t => t.Id), firstPage.Select(t => t.Id));
    }

    [Fact]
    public async Task The_period_bounds_are_inclusive_on_both_ends()
    {
        await SeedPeriodAsync();
        var service = CreateService();

        var (items, _) = await service.GetTripsAsync(new TripFilter(From: PeriodStart, To: PeriodEnd));

        Assert.Contains(items, t => t.ServiceDate == PeriodStart);
        Assert.Contains(items, t => t.ServiceDate == PeriodEnd);
    }

    private ITripReadService CreateService() =>
        new TripReadService(fixture.CreateTripsContext(PostgresFixture.TenantA));

    /// <summary>
    /// Deliberately inserted out of order so the ordering assertions prove the query sorts,
    /// rather than accidentally reading rows back in insertion order.
    /// </summary>
    private async Task SeedPeriodAsync()
    {
        await using var context = fixture.CreateTripsContext(PostgresFixture.TenantA);

        if (await context.TripReadModels.AnyAsync(t => t.ServiceDate >= PeriodStart && t.ServiceDate <= PeriodEnd))
        {
            return;
        }

        TripReadModel Row(string number, DateOnly date, TimeOnly windowStart, TripStatus status, Guid? driverId) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = PostgresFixture.TenantA,
            TripNumber = number,
            ServiceDate = date,
            WindowStart = windowStart,
            WindowEnd = null,
            ServiceType = nameof(TripServiceType.Charter),
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
            ClientId = null,
            ClientName = null,
            PoNumber = null,
            DriverId = driverId,
            DriverName = driverId is null ? null : "Test Driver",
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

        var driver = Guid.NewGuid();
        var mid = new DateOnly(2031, 3, 15);

        context.TripReadModels.AddRange(
            // Same day, later window — must sort after PG-2031-02 despite being added first.
            Row("PG-2031-03", mid, new TimeOnly(14, 30), TripStatus.Scheduled, driver),
            Row("PG-2031-02", mid, new TimeOnly(6, 30), TripStatus.Scheduled, null),
            // Same day, same window — the trip number is the tiebreaker that makes paging stable.
            Row("PG-2031-04", mid, new TimeOnly(14, 30), TripStatus.Completed, driver),
            Row("PG-2031-07", PeriodEnd, new TimeOnly(9, 0), TripStatus.Cancelled, null),
            Row("PG-2031-01", PeriodStart, new TimeOnly(8, 0), TripStatus.Scheduled, null),
            Row("PG-2031-06", new DateOnly(2031, 3, 22), new TimeOnly(7, 15), TripStatus.Scheduled, driver),
            Row("PG-2031-05", new DateOnly(2031, 3, 22), new TimeOnly(7, 0), TripStatus.InProgress, driver));

        await context.SaveChangesAsync();
    }
}
