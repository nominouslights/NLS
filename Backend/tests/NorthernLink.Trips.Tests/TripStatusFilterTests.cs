using NorthernLink.Trips.Application.Trips.GetTrips;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The rules for <c>?status=</c> / <c>?statuses=</c> on <c>GET /api/trips</c>. The single
/// filter's behaviour is pinned unchanged; the set is strict — exact member names, never an
/// ordinal, never an empty set read as "no filter", never alongside the single filter.
/// </summary>
public class TripStatusFilterTests
{
    // ---- ?status= (unchanged) ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void No_status_at_all_is_no_narrowing(string? status)
    {
        var result = TripStatusFilter.Parse(status, statuses: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatusFilter.None, result.Value);
    }

    [Fact]
    public void A_single_status_name_parses_to_Status_and_leaves_Statuses_null()
    {
        var result = TripStatusFilter.Parse("ReadyForBilling", statuses: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(TripStatus.ReadyForBilling, result.Value.Status);
        Assert.Null(result.Value.Statuses);
    }

    [Theory]
    [InlineData("Bogus")]
    [InlineData("scheduled")] // ordinal, as it always was
    [InlineData("2")]         // an ordinal would bind by position
    [InlineData(" Scheduled")]
    public void A_single_status_that_is_not_a_member_name_is_InvalidStatusFilter(string status)
    {
        var result = TripStatusFilter.Parse(status, statuses: null);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.InvalidStatusFilter, result.Error);
    }

    // ---- ?statuses= ----

    [Fact]
    public void A_comma_separated_set_parses_in_order_to_Statuses_and_leaves_Status_null()
    {
        var result = TripStatusFilter.Parse(null, "Scheduled,InProgress,ReadyForBilling,Invoiced");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Status);
        Assert.Equal(
            [TripStatus.Scheduled, TripStatus.InProgress, TripStatus.ReadyForBilling, TripStatus.Invoiced],
            result.Value.Statuses);
    }

    [Fact]
    public void A_one_member_set_is_still_a_set()
    {
        var result = TripStatusFilter.Parse(null, "Invoiced");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Status);
        Assert.Equal([TripStatus.Invoiced], result.Value.Statuses);
    }

    [Fact]
    public void Duplicates_collapse()
    {
        var result = TripStatusFilter.Parse(null, "Scheduled,Invoiced,Scheduled");

        Assert.True(result.IsSuccess);
        Assert.Equal([TripStatus.Scheduled, TripStatus.Invoiced], result.Value.Statuses);
    }

    [Fact]
    public void Every_member_name_is_accepted()
    {
        var result = TripStatusFilter.Parse(null, string.Join(',', Enum.GetNames<TripStatus>()));

        Assert.True(result.IsSuccess);
        Assert.Equal(Enum.GetValues<TripStatus>().Length, result.Value.Statuses!.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(",")]
    [InlineData(" , ,")]
    public void A_set_naming_nothing_is_EmptyStatusesFilter_not_no_filter(string statuses)
    {
        var result = TripStatusFilter.Parse(null, statuses);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.EmptyStatusesFilter, result.Error);
    }

    [Theory]
    [InlineData("Scheduled,Bogus")]
    [InlineData("Scheduled,invoiced")]     // ordinal
    [InlineData("Scheduled,2")]            // never an ordinal
    [InlineData("Scheduled, InProgress")]  // exact names: no trimming
    [InlineData("Scheduled,,Invoiced")]    // an empty segment is not a status
    [InlineData("Scheduled,")]             // nor is a trailing one
    public void Any_segment_that_is_not_a_member_name_is_InvalidStatusFilter(string statuses)
    {
        var result = TripStatusFilter.Parse(null, statuses);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.InvalidStatusFilter, result.Error);
    }

    // ---- both ----

    [Theory]
    [InlineData("Scheduled", "InProgress")]
    [InlineData("Scheduled", "Scheduled")] // even when they agree
    [InlineData("Bogus", "Scheduled")]     // the conflict is reported before either is parsed
    [InlineData("Scheduled", "")]
    public void Both_filters_together_is_ConflictingStatusFilters(string status, string statuses)
    {
        var result = TripStatusFilter.Parse(status, statuses);

        Assert.True(result.IsFailure);
        Assert.Equal(TripErrors.ConflictingStatusFilters, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_status_beside_a_set_is_absent_as_it_always_was(string status)
    {
        var result = TripStatusFilter.Parse(status, "Scheduled,Invoiced");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Status);
        Assert.Equal([TripStatus.Scheduled, TripStatus.Invoiced], result.Value.Statuses);
    }
}
