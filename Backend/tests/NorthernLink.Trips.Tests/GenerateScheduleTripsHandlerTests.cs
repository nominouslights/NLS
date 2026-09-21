using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Application.Schedules.GenerateTrips;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Schedules;
using NorthernLink.Trips.Domain.Trips;
using Xunit;

namespace NorthernLink.Trips.Tests;

/// <summary>
/// The on-demand generate command and its preview query, driven through the real
/// <see cref="ScheduleTripMaterializer"/> over in-memory fakes with "today" pinned to
/// <see cref="TestPlanning.Monday"/>. This is also the worker's per-template path — the
/// worker itself stays untested (its cross-tenant enumeration needs a real DbContext).
/// </summary>
public class GenerateScheduleTripsHandlerTests
{
    private static readonly DateOnly Today = TestPlanning.Monday;

    private readonly FakeScheduleTemplateRepository _templates = new();
    private readonly FakeRouteRepository _routes = new();
    private readonly FakeDriverLookupRepository _drivers = new();
    private readonly FakeVehicleLookupRepository _vehicles = new();
    private readonly FakeTripRepository _trips = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 7, 20, 14, 0, 0, TimeSpan.Zero));

    private readonly Route _route = TestPlanning.CreateRoute();

    public GenerateScheduleTripsHandlerTests()
    {
        _routes.Add(_route);
        _drivers.Drivers.Add(TestPlanning.ActiveDriver());
        _vehicles.Vehicles.Add(TestPlanning.ActiveVehicle(seats: 14));
    }

    private ScheduleTripMaterializer Materializer(ITripRepository? trips = null) =>
        new(_templates, _routes, _drivers, _vehicles, trips ?? _trips, new FakeTripNumberGenerator());

    private GenerateScheduleTripsCommandHandler Generate(ITripRepository? trips = null) =>
        new(Materializer(trips), _clock);

    private PreviewScheduleTripGenerationQueryHandler Preview() => new(Materializer(), _clock);

    /// <summary>
    /// A weekday round-trip template (06:30 out / 17:30 back) on the fake route, defaulting
    /// to the baseline driver and unit. <paramref name="withDefaultDriver"/> false leaves the
    /// driver unset; <paramref name="defaultDriverId"/> substitutes a different one.
    /// </summary>
    private ScheduleTemplate WeekdayRoundTrip(
        bool withDefaultDriver = true,
        Guid? defaultDriverId = null,
        string? defaultVehicleUnit = TestPlanning.VehicleUnit,
        bool active = true,
        TripServiceType serviceType = TripServiceType.ContractCrew,
        int? seatsCapacity = 12)
    {
        var template = TestPlanning.CreateTemplate(
            departureTime: new TimeOnly(6, 30),
            returnDepartureTime: new TimeOnly(17, 30),
            generationHorizonDays: 7,
            active: active,
            routeId: _route.Id,
            serviceType: serviceType,
            seatsCapacity: seatsCapacity,
            defaultDriverId: withDefaultDriver ? defaultDriverId ?? TestPlanning.DriverId : null,
            defaultVehicleUnit: defaultVehicleUnit);
        _templates.Add(template);
        return template;
    }

    private static GenerateScheduleTripsCommand Command(ScheduleTemplate template, DateOnly through) =>
        new(TestPlanning.TenantId, template.Id, through);

    private static PreviewScheduleTripGenerationQuery Query(ScheduleTemplate template, DateOnly through) =>
        new(TestPlanning.TenantId, template.Id, through);

    // ----- Happy path -----

    [Fact]
    public async Task Generates_every_leg_through_the_chosen_date_in_one_save()
    {
        var template = WeekdayRoundTrip();
        var through = Today.AddDays(13); // Sunday of week 2 → two full weeks of weekdays

        var result = await Generate().Handle(Command(template, through), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Message);
        Assert.Equal(new ScheduleTripGenerationResult(
            Today, through, TripCount: 20, AlreadyExisted: 0, Outbound: 10, Inbound: 10,
            FirstServiceDate: Today, LastServiceDate: Today.AddDays(11)), result.Value);
        Assert.Equal(20, _trips.Trips.Count);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Generated_trips_carry_the_template_provenance_and_the_default_assignment()
    {
        var template = WeekdayRoundTrip();

        await Generate().Handle(Command(template, Today.AddDays(4)), CancellationToken.None);

        Assert.Equal(10, _trips.Trips.Count);
        Assert.All(_trips.Trips, trip =>
        {
            Assert.Equal(TestPlanning.TenantId, trip.TenantId);
            Assert.Equal(template.Id, trip.ScheduleTemplateId);
            Assert.Equal(TripGenerator.RoundTripKeyFor(template.Id, trip.ServiceDate), trip.RoundTripKey);
            Assert.NotNull(trip.Direction);
            Assert.Equal(TripStatus.Scheduled, trip.Status);
            Assert.Equal(TestPlanning.DriverId, trip.DriverId);
            Assert.Equal(TestPlanning.DriverName, trip.DriverName);
            Assert.Equal(TestPlanning.VehicleId, trip.VehicleId);
            Assert.Equal(TestPlanning.VehicleUnit, trip.VehicleUnit);
            // The fleet vehicle's capacity, not the template's manual 12.
            Assert.Equal(14, trip.SeatsCapacity);
            Assert.Equal(_route.Id, trip.RouteId);
            Assert.Equal("Alamos Gold", trip.ClientName);
            Assert.Null(trip.PoNumber);
            Assert.False(trip.IsEmptyLeg);
        });

        var outbound = _trips.Trips.First(t => t.Direction == TripDirection.Outbound);
        Assert.Equal(new TimeOnly(6, 30), outbound.WindowStart);
        Assert.Equal(new TimeOnly(8, 15), outbound.WindowEnd); // + the route's 105 minutes
        Assert.Equal(_route.Origin, outbound.Origin);
        Assert.Equal(_route.Destination, outbound.Destination);

        var inbound = _trips.Trips.First(t => t.Direction == TripDirection.Inbound);
        Assert.Equal(new TimeOnly(17, 30), inbound.WindowStart);
        Assert.Equal(_route.Destination, inbound.Origin);
        Assert.Equal(_route.Origin, inbound.Destination);
        Assert.Equal(_route.Stops.Count, inbound.Stops.Count);
        Assert.Equal("Lynn Lake", inbound.Stops.OrderBy(s => s.Order).First().Name);

        // Every occurrence key is unique — the unique index would have rejected otherwise.
        Assert.Equal(10, _trips.Trips.Select(t => (t.ServiceDate, t.Direction)).Distinct().Count());
        Assert.Equal(10, _trips.Trips.Select(t => t.TripNumber).Distinct().Count());
    }

    [Fact]
    public async Task A_second_run_over_the_same_window_creates_nothing_and_does_not_save()
    {
        var template = WeekdayRoundTrip();
        var through = Today.AddDays(13);
        await Generate().Handle(Command(template, through), CancellationToken.None);

        var result = await Generate().Handle(Command(template, through), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.TripCount);
        Assert.Equal(20, result.Value.AlreadyExisted);
        Assert.Equal(0, result.Value.Outbound);
        Assert.Equal(0, result.Value.Inbound);
        Assert.Null(result.Value.FirstServiceDate);
        Assert.Null(result.Value.LastServiceDate);
        Assert.Equal(20, _trips.Trips.Count);
        Assert.Equal(1, _trips.SaveCount);
    }

    [Fact]
    public async Task Partially_generated_window_fills_only_the_missing_occurrences()
    {
        var template = WeekdayRoundTrip();
        // The worker already made Monday's pair.
        _trips.Add(TestPlanning.ScheduleTrip(
            "TR-1", scheduleTemplateId: template.Id, serviceDate: Today, direction: TripDirection.Outbound).Value);
        _trips.Add(TestPlanning.ScheduleTrip(
            "TR-2", scheduleTemplateId: template.Id, serviceDate: Today, direction: TripDirection.Inbound).Value);

        var result = await Generate().Handle(Command(template, Today.AddDays(4)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.Value.TripCount);
        Assert.Equal(2, result.Value.AlreadyExisted);
        Assert.Equal(Today.AddDays(1), result.Value.FirstServiceDate);
        Assert.Equal(10, _trips.Trips.Count);
        // Monday's pair is still the pre-existing TR-1/TR-2, not re-minted numbers.
        Assert.Equal(2, _trips.Trips.Count(t => t.ServiceDate == Today));
        Assert.All(_trips.Trips.Where(t => t.ServiceDate == Today), t => Assert.StartsWith("TR-", t.TripNumber));
        Assert.DoesNotContain(_trips.Trips, t => t.ServiceDate == Today && t.TripNumber.StartsWith("TR-5", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Extending_the_window_generates_only_the_new_tail()
    {
        var template = WeekdayRoundTrip();
        await Generate().Handle(Command(template, Today.AddDays(6)), CancellationToken.None);

        var result = await Generate().Handle(Command(template, Today.AddDays(13)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.TripCount);
        Assert.Equal(10, result.Value.AlreadyExisted);
        Assert.Equal(Today.AddDays(7), result.Value.FirstServiceDate);
        Assert.Equal(20, _trips.Trips.Count);
        Assert.Equal(2, _trips.SaveCount);
    }

    // ----- The overnight pad -----

    [Fact]
    public async Task Overnight_return_on_the_last_day_lands_past_the_window_and_is_seen_on_the_next_run()
    {
        // Fridays only, 17:30 out, back 06:30 the next morning.
        var template = TestPlanning.CreateTemplate(
            daysOfWeek: [DayOfWeek.Friday],
            departureTime: new TimeOnly(17, 30),
            returnDepartureTime: new TimeOnly(6, 30),
            returnNextDay: true,
            generationHorizonDays: 7,
            routeId: _route.Id,
            defaultDriverId: TestPlanning.DriverId);
        _templates.Add(template);
        var friday = Today.AddDays(4);

        var first = await Generate().Handle(Command(template, friday), CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Message);
        Assert.Equal(2, first.Value.TripCount);
        Assert.Equal(friday.AddDays(1), first.Value.LastServiceDate); // Saturday's inbound, one day past "through"
        Assert.Contains(_trips.Trips, t => t.ServiceDate == friday.AddDays(1) && t.Direction == TripDirection.Inbound);

        // Re-run through the same Friday: without the one-day pad on the existing-keys query
        // the Saturday inbound would be re-emitted and collide on the unique index.
        var again = await Generate().Handle(Command(template, friday), CancellationToken.None);
        Assert.True(again.IsSuccess, again.Error.Message);
        Assert.Equal(0, again.Value.TripCount);
        Assert.Equal(2, again.Value.AlreadyExisted);

        // And the worker's own window (through: null ⇒ the 7-day horizon) collides with nothing.
        var worker = await Materializer().PlanAsync(template.Id, Today, through: null, CancellationToken.None);
        Assert.True(worker.IsSuccess, worker.Error.Message);
        Assert.Empty(worker.Value.Drafts);
        Assert.Equal(2, worker.Value.AlreadyExisted);
        Assert.Equal(Today.AddDays(6), worker.Value.Through);

        Assert.Equal(2, _trips.Trips.Count);
        Assert.Equal(1, _trips.SaveCount);
    }

    // ----- Template and window guards -----

    [Fact]
    public async Task Inactive_template_is_refused()
    {
        var template = WeekdayRoundTrip(active: false);

        var result = await Generate().Handle(Command(template, Today.AddDays(6)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ScheduleTemplateErrors.TemplateInactive, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Fact]
    public async Task Unknown_template_is_not_found()
    {
        var result = await Generate().Handle(
            new GenerateScheduleTripsCommand(TestPlanning.TenantId, Guid.NewGuid(), Today.AddDays(6)),
            CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.NotFound, result.Error);
    }

    [Fact]
    public async Task Through_before_today_is_an_invalid_window()
    {
        var template = WeekdayRoundTrip();

        var result = await Generate().Handle(Command(template, Today.AddDays(-1)), CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.InvalidGenerationWindow, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Fact]
    public async Task Through_beyond_the_cap_is_an_invalid_window()
    {
        var template = WeekdayRoundTrip();

        var result = await Generate().Handle(
            Command(template, Today.AddDays(ScheduleTemplate.MaxGenerateAheadDays + 1)), CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.InvalidGenerationWindow, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Fact]
    public async Task Through_exactly_at_the_cap_is_accepted()
    {
        var template = WeekdayRoundTrip();
        var through = Today.AddDays(ScheduleTemplate.MaxGenerateAheadDays);

        var result = await Generate().Handle(Command(template, through), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Message);
        Assert.Equal(through, result.Value.Through);
        Assert.True(result.Value.TripCount > 500); // ~52 weeks × 5 weekdays × 2 legs
        Assert.Equal(result.Value.TripCount, _trips.Trips.Count);
    }

    [Fact]
    public async Task Today_alone_is_a_valid_one_day_window()
    {
        var template = WeekdayRoundTrip();

        var result = await Generate().Handle(Command(template, Today), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Message);
        Assert.Equal(2, result.Value.TripCount);
        Assert.Equal(Today, result.Value.From);
        Assert.Equal(Today, result.Value.Through);
    }

    [Fact]
    public async Task Missing_route_is_refused_before_anything_is_generated()
    {
        var template = TestPlanning.CreateTemplate(
            routeId: Guid.NewGuid(), defaultDriverId: TestPlanning.DriverId);
        _templates.Add(template);

        var result = await Generate().Handle(Command(template, Today.AddDays(6)), CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.RouteMissing, result.Error);
        Assert.Empty(_trips.Trips);
    }

    // ----- Assignment guards -----

    [Fact]
    public async Task Template_without_a_default_driver_is_refused_and_nothing_is_saved()
    {
        var template = WeekdayRoundTrip(withDefaultDriver: false);

        var result = await Generate().Handle(Command(template, Today.AddDays(6)), CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.NoDefaultDriver, result.Error);
        Assert.Empty(_trips.Trips);
        Assert.Equal(0, _trips.SaveCount);
    }

    [Fact]
    public async Task Default_driver_missing_from_the_lookup_is_unavailable()
    {
        var template = WeekdayRoundTrip(defaultDriverId: Guid.NewGuid());

        var result = await Generate().Handle(Command(template, Today.AddDays(6)), CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.DefaultDriverUnavailable, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Fact]
    public async Task Deactivated_default_driver_is_unavailable()
    {
        _drivers.Drivers.Clear();
        _drivers.Drivers.Add(TestPlanning.ActiveDriver(status: "Deactivated"));
        var template = WeekdayRoundTrip();

        var result = await Generate().Handle(Command(template, Today.AddDays(6)), CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.DefaultDriverUnavailable, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Fact]
    public async Task Template_without_a_default_vehicle_unit_is_refused()
    {
        var template = WeekdayRoundTrip(defaultVehicleUnit: null);

        var result = await Generate().Handle(Command(template, Today.AddDays(6)), CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.NoDefaultVehicle, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Fact]
    public async Task Default_unit_not_in_the_fleet_is_unavailable()
    {
        var template = WeekdayRoundTrip(defaultVehicleUnit: "U-99");

        var result = await Generate().Handle(Command(template, Today.AddDays(6)), CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.DefaultVehicleUnavailable, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Fact]
    public async Task Default_unit_out_of_service_is_unavailable()
    {
        _vehicles.Vehicles.Clear();
        _vehicles.Vehicles.Add(TestPlanning.ActiveVehicle(status: "OutOfService"));
        var template = WeekdayRoundTrip();

        var result = await Generate().Handle(Command(template, Today.AddDays(6)), CancellationToken.None);

        Assert.Equal(ScheduleTemplateErrors.DefaultVehicleUnavailable, result.Error);
        Assert.Empty(_trips.Trips);
    }

    [Fact]
    public async Task Assignment_guards_are_skipped_when_nothing_new_would_be_created()
    {
        // Fully generated through the window, then the driver is deactivated: a re-run must
        // report "0 new" rather than fail — exactly as the worker skips a topped-up template.
        var template = WeekdayRoundTrip();
        var through = Today.AddDays(6);
        await Generate().Handle(Command(template, through), CancellationToken.None);
        _drivers.Drivers.Clear();
        _vehicles.Vehicles.Clear();

        var result = await Generate().Handle(Command(template, through), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Message);
        Assert.Equal(0, result.Value.TripCount);
        Assert.Equal(10, result.Value.AlreadyExisted);
    }

    // ----- Save-time race -----

    [Fact]
    public async Task Unique_index_rejection_on_save_is_a_generation_conflict()
    {
        var template = WeekdayRoundTrip();
        // A concurrent run materialized Tuesday's outbound after this run's existing-keys
        // read: the blind repository hides it from the plan, so the save collides.
        _trips.Add(TestPlanning.ScheduleTrip(
            "TR-1", scheduleTemplateId: template.Id, serviceDate: Today.AddDays(1), direction: TripDirection.Outbound).Value);

        var result = await Generate(new BlindTripRepository(_trips))
            .Handle(Command(template, Today.AddDays(4)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ScheduleTemplateErrors.GenerationConflict, result.Error);
        Assert.Single(_trips.Trips); // nothing from this run persisted
        Assert.Equal(0, _trips.SaveCount);
    }

    // ----- Cargo -----

    [Fact]
    public async Task Cargo_template_generates_seatless_trips()
    {
        var template = WeekdayRoundTrip(serviceType: TripServiceType.Cargo, seatsCapacity: null);

        var result = await Generate().Handle(Command(template, Today.AddDays(4)), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Message);
        Assert.Equal(10, _trips.Trips.Count);
        Assert.All(_trips.Trips, trip =>
        {
            Assert.Equal(TripServiceType.Cargo, trip.ServiceType);
            Assert.Null(trip.SeatsCapacity);
            Assert.Null(trip.SeatsMinimum);
        });
    }

    // ----- Preview -----

    [Fact]
    public async Task Preview_reports_the_same_counts_as_a_generate_without_persisting()
    {
        var template = WeekdayRoundTrip();
        var through = Today.AddDays(13);

        var preview = await Preview().Handle(Query(template, through), CancellationToken.None);

        Assert.True(preview.IsSuccess, preview.Error.Message);
        Assert.Empty(_trips.Trips);
        Assert.Equal(0, _trips.SaveCount);

        var generated = await Generate().Handle(Command(template, through), CancellationToken.None);
        Assert.Equal(generated.Value, preview.Value);
    }

    [Fact]
    public async Task Preview_after_generation_shows_everything_as_already_existing()
    {
        var template = WeekdayRoundTrip();
        var through = Today.AddDays(6);
        await Generate().Handle(Command(template, through), CancellationToken.None);

        var preview = await Preview().Handle(Query(template, through), CancellationToken.None);

        Assert.True(preview.IsSuccess);
        Assert.Equal(0, preview.Value.TripCount);
        Assert.Equal(10, preview.Value.AlreadyExisted);
        Assert.Null(preview.Value.FirstServiceDate);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("no-driver")]
    [InlineData("no-vehicle")]
    [InlineData("window")]
    public async Task Preview_surfaces_the_same_guard_errors(string scenario)
    {
        var template = scenario switch
        {
            "inactive" => WeekdayRoundTrip(active: false),
            "no-driver" => WeekdayRoundTrip(defaultDriverId: Guid.NewGuid()),
            "no-vehicle" => WeekdayRoundTrip(defaultVehicleUnit: "U-99"),
            _ => WeekdayRoundTrip(),
        };
        var through = scenario == "window" ? Today.AddDays(400) : Today.AddDays(6);

        var preview = await Preview().Handle(Query(template, through), CancellationToken.None);
        var generate = await Generate().Handle(Command(template, through), CancellationToken.None);

        Assert.True(preview.IsFailure);
        Assert.Equal(generate.Error, preview.Error);
        Assert.Empty(_trips.Trips);
    }

    [Fact]
    public async Task Handlers_take_today_from_the_clock_in_utc()
    {
        // 23:30 Winnipeg on Sunday the 19th is already Monday the 20th UTC — the worker's convention.
        _clock.UtcNow = new DateTimeOffset(2026, 7, 20, 4, 30, 0, TimeSpan.Zero);
        var template = WeekdayRoundTrip();

        var result = await Generate().Handle(Command(template, Today), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Message);
        Assert.Equal(Today, result.Value.From);
    }

    /// <summary>
    /// Delegates to the shared fake but blinds the existing-keys read, so a trip that "landed
    /// between the read and the save" collides at save time — forcing the 23505 path.
    /// </summary>
    private sealed class BlindTripRepository(FakeTripRepository inner) : ITripRepository
    {
        public void Add(Trip trip) => inner.Add(trip);

        public Task<Trip?> GetByIdAsync(Guid tripId, CancellationToken cancellationToken = default) =>
            inner.GetByIdAsync(tripId, cancellationToken);

        public Task<Trip?> GetByTripNumberAsync(string tripNumber, CancellationToken cancellationToken = default) =>
            inner.GetByTripNumberAsync(tripNumber, cancellationToken);

        public Task<IReadOnlyList<Trip>> GetByRoundTripKeyAsync(string roundTripKey, CancellationToken cancellationToken = default) =>
            inner.GetByRoundTripKeyAsync(roundTripKey, cancellationToken);

        public Task<IReadOnlyList<Trip>> GetByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> tripIds, CancellationToken cancellationToken = default) =>
            inner.GetByIdsAsync(tenantId, tripIds, cancellationToken);

        public Task<Trip?> GetByBookingDayIdAsync(Guid tenantId, Guid bookingDayId, CancellationToken cancellationToken = default) =>
            inner.GetByBookingDayIdAsync(tenantId, bookingDayId, cancellationToken);

        public Task<bool> TryAddForBookingDayAsync(Trip trip, CancellationToken cancellationToken = default) =>
            inner.TryAddForBookingDayAsync(trip, cancellationToken);

        public Task<IReadOnlySet<(DateOnly ServiceDate, TripDirection Direction)>> GetGeneratedOccurrenceKeysAsync(
            Guid templateId, DateOnly from, DateOnly toExclusive, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<(DateOnly ServiceDate, TripDirection Direction)>>(
                new HashSet<(DateOnly ServiceDate, TripDirection Direction)>());

        public Task<bool> TryAddGeneratedAsync(IReadOnlyList<Trip> trips, CancellationToken cancellationToken = default) =>
            inner.TryAddGeneratedAsync(trips, cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            inner.SaveChangesAsync(cancellationToken);
    }
}
