using NorthernLink.Trips.Application.Schedules.GenerateTrips;
using NorthernLink.Trips.Domain.Manifests;
using Xunit;

namespace NorthernLink.Trips.Tests;

public class TripGeneratorTests
{
    private static readonly HashSet<(DateOnly, TripDirection)> NoExisting = [];

    [Fact]
    public void Expands_every_matching_day_across_the_horizon()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday],
            generationHorizonDays: 7);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(7, drafts.Count);
        Assert.Equal(TestPlanning.Monday, drafts[0].ServiceDate);
        Assert.Equal(TestPlanning.Monday.AddDays(6), drafts[^1].ServiceDate);
        Assert.All(drafts, draft => Assert.Equal(TripDirection.Outbound, draft.Direction));
    }

    [Fact]
    public void Skips_days_the_template_does_not_run()
    {
        var template = TestPlanning.CreateTemplate(generationHorizonDays: 7); // weekdays only

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(5, drafts.Count); // Mon–Fri of the week starting Monday
        Assert.DoesNotContain(drafts, draft => draft.ServiceDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    [Fact]
    public void Horizon_bounds_the_expansion()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday],
            generationHorizonDays: 3);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(3, drafts.Count);
        Assert.All(drafts, draft => Assert.InRange(
            draft.ServiceDate, TestPlanning.Monday, TestPlanning.Monday.AddDays(2)));
    }

    [Fact]
    public void Round_trip_template_pairs_outbound_and_return_legs_with_a_shared_key()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday],
            departureTime: new TimeOnly(6, 30),
            returnDepartureTime: new TimeOnly(17, 30),
            generationHorizonDays: 7);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        Assert.Equal(2, drafts.Count);
        var outbound = Assert.Single(drafts, draft => draft.Direction == TripDirection.Outbound);
        var inbound = Assert.Single(drafts, draft => draft.Direction == TripDirection.Inbound);

        Assert.Equal(new TimeOnly(6, 30), outbound.DepartureTime);
        Assert.Equal(new TimeOnly(17, 30), inbound.DepartureTime);
        Assert.Equal(outbound.ServiceDate, inbound.ServiceDate);

        Assert.NotNull(outbound.RoundTripKey);
        Assert.Equal(outbound.RoundTripKey, inbound.RoundTripKey);
        Assert.Equal($"{template.Id:N}:{TestPlanning.Monday:yyyyMMdd}", outbound.RoundTripKey);
    }

    [Fact]
    public void One_way_template_has_no_round_trip_key()
    {
        var template = TestPlanning.CreateTemplate(daysOfWeek: [DayOfWeek.Monday], generationHorizonDays: 7);

        var drafts = TripGenerator.Generate(template, NoExisting, TestPlanning.Monday);

        var draft = Assert.Single(drafts);
        Assert.Null(draft.RoundTripKey);
    }

    [Fact]
    public void Already_materialized_occurrences_are_skipped()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday],
            returnDepartureTime: new TimeOnly(17, 30),
            generationHorizonDays: 7);

        var existing = new HashSet<(DateOnly, TripDirection)>
        {
            (TestPlanning.Monday, TripDirection.Outbound),
        };

        var drafts = TripGenerator.Generate(template, existing, TestPlanning.Monday);

        var draft = Assert.Single(drafts);
        Assert.Equal(TripDirection.Inbound, draft.Direction);
    }

    [Fact]
    public void Fully_materialized_template_generates_nothing()
    {
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Monday],
            returnDepartureTime: new TimeOnly(17, 30),
            generationHorizonDays: 7);

        var existing = new HashSet<(DateOnly, TripDirection)>
        {
            (TestPlanning.Monday, TripDirection.Outbound),
            (TestPlanning.Monday, TripDirection.Inbound),
        };

        Assert.Empty(TripGenerator.Generate(template, existing, TestPlanning.Monday));
    }

    [Fact]
    public void Inactive_template_generates_nothing()
    {
        var template = TestPlanning.CreateTemplate(active: false);

        Assert.Empty(TripGenerator.Generate(template, NoExisting, TestPlanning.Monday));
    }
}
