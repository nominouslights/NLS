using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Application.Integration;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Schedules;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Tests;

/// <summary>Factory helpers for the trip-planning aggregates with valid baseline payloads.</summary>
internal static class TestPlanning
{
    public static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>Baseline assignment — a trip is never created unassigned, so the helper always has one.</summary>
    public static readonly Guid DriverId = Guid.Parse("00000000-0000-0000-0000-0000000000d1");
    public const string DriverName = "R. Ballantyne";
    public static readonly Guid VehicleId = Guid.Parse("00000000-0000-0000-0000-0000000000e1");
    public const string VehicleUnit = "U-04";

    /// <summary>Monday, 20 July 2026 — a fixed "today" for generator tests.</summary>
    public static readonly DateOnly Monday = new(2026, 7, 20);

    public static List<RouteStop> Stops() =>
    [
        new RouteStop { Name = "Thompson", Order = 0 },
        new RouteStop { Name = "Leaf Rapids", Order = 1 },
        new RouteStop { Name = "Lynn Lake", Order = 2 },
    ];

    public static Route CreateRoute() =>
        Route.Create(
            TenantId,
            "Thompson ↔ Lynn Lake",
            Stops(),
            distanceKm: 320,
            estimatedDuration: TimeSpan.FromMinutes(105),
            requiredLicenceClass: "Class 4").Value;

    public static ScheduleTemplate CreateTemplate(
        IReadOnlyList<DayOfWeek>? daysOfWeek = null,
        TimeOnly? departureTime = null,
        TimeOnly? returnDepartureTime = null,
        bool returnNextDay = false,
        int generationHorizonDays = 7,
        bool active = true,
        Guid? routeId = null,
        ScheduleRecurrenceKind recurrenceKind = ScheduleRecurrenceKind.DaysOfWeek,
        int? intervalDays = null,
        DateOnly? anchorDate = null,
        IReadOnlyList<int>? daysOfMonth = null,
        TripServiceType serviceType = TripServiceType.ContractCrew,
        int? seatsCapacity = 12,
        int? seatsMinimum = null,
        Guid? defaultDriverId = null,
        string? defaultVehicleUnit = VehicleUnit)
    {
        var result = CreateTemplateResult(
            daysOfWeek, departureTime, returnDepartureTime, returnNextDay, generationHorizonDays,
            routeId, recurrenceKind, intervalDays, anchorDate, daysOfMonth,
            serviceType, seatsCapacity, seatsMinimum, defaultDriverId, defaultVehicleUnit);
        var template = result.Value;

        if (!active)
        {
            template.Deactivate();
        }

        return template;
    }

    /// <summary>
    /// Builds a template <see cref="Result{T}"/> without unwrapping it — for validation tests that
    /// assert on the failure <see cref="Error"/> rather than a materialized aggregate.
    /// </summary>
    public static Result<ScheduleTemplate> CreateTemplateResult(
        IReadOnlyList<DayOfWeek>? daysOfWeek = null,
        TimeOnly? departureTime = null,
        TimeOnly? returnDepartureTime = null,
        bool returnNextDay = false,
        int generationHorizonDays = 7,
        Guid? routeId = null,
        ScheduleRecurrenceKind recurrenceKind = ScheduleRecurrenceKind.DaysOfWeek,
        int? intervalDays = null,
        DateOnly? anchorDate = null,
        IReadOnlyList<int>? daysOfMonth = null,
        TripServiceType serviceType = TripServiceType.ContractCrew,
        int? seatsCapacity = 12,
        int? seatsMinimum = null,
        Guid? defaultDriverId = null,
        string? defaultVehicleUnit = VehicleUnit) =>
        ScheduleTemplate.Create(
            TenantId,
            "Alamos crew shuttle",
            routeId ?? Guid.NewGuid(),
            serviceType,
            clientId: null,
            clientName: "Alamos Gold",
            recurrenceKind: recurrenceKind,
            daysOfWeek: daysOfWeek ?? [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            intervalDays: intervalDays,
            anchorDate: anchorDate,
            daysOfMonth: daysOfMonth ?? [],
            departureTime: departureTime ?? new TimeOnly(6, 30),
            returnDepartureTime: returnDepartureTime,
            returnNextDay: returnNextDay,
            seatsCapacity: seatsCapacity,
            seatsMinimum: seatsMinimum,
            defaultVehicleUnit: defaultVehicleUnit,
            defaultDriverId: defaultDriverId,
            generationHorizonDays: generationHorizonDays);

    /// <summary>The baseline driver as an Active driver_lookup row — what a template's default driver resolves to.</summary>
    public static DriverLookup ActiveDriver(string status = DriverLookup.ActiveStatus) => new()
    {
        DriverId = DriverId,
        TenantId = TenantId,
        Name = DriverName,
        LicenceClass = "Class 4",
        Status = status,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    /// <summary>The baseline unit as a vehicle_lookup row; <paramref name="seats"/> is what generated trips snapshot.</summary>
    public static VehicleLookup ActiveVehicle(int seats = 12, string status = VehicleLookup.ActiveStatus) => new()
    {
        VehicleId = VehicleId,
        TenantId = TenantId,
        UnitNumber = VehicleUnit,
        Status = status,
        RequiredLicenceClass = "Class 4",
        SeatingCapacity = seats,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// A valid scheduled trip; override the arguments a test cares about. Null
    /// driver/vehicle arguments fall back to the baseline assignment — creation without
    /// one is a domain error, exercised directly in the lifecycle tests.
    /// </summary>
    public static Result<Trip> ScheduleTrip(
        string tripNumber = "TR-1001",
        Guid? driverId = null,
        string? driverName = null,
        Guid? vehicleId = null,
        string? vehicleUnit = null,
        int? seatsCapacity = 12,
        Guid? scheduleTemplateId = null,
        string? roundTripKey = null,
        TripDirection? direction = null,
        Guid? tenantId = null,
        Guid? clientId = null,
        DateOnly? serviceDate = null,
        TimeOnly? windowStart = null,
        string origin = "Thompson",
        string destination = "Lynn Lake",
        bool isEmptyLeg = false,
        TripServiceType serviceType = TripServiceType.ContractCrew) =>
        Trip.Schedule(
            tenantId ?? TenantId,
            tripNumber,
            serviceDate: serviceDate ?? new DateOnly(2026, 7, 21),
            windowStart: windowStart ?? new TimeOnly(6, 30),
            windowEnd: new TimeOnly(8, 15),
            serviceType,
            routeId: null,
            routeName: "Thompson ↔ Lynn Lake",
            origin: origin,
            destination: destination,
            stops: Stops(),
            distanceKm: 320,
            scheduleTemplateId: scheduleTemplateId,
            roundTripKey: roundTripKey,
            direction: direction,
            isEmptyLeg: isEmptyLeg,
            clientId: clientId,
            clientName: "Alamos Gold",
            poNumber: "PO-2026-118",
            driverId: driverId ?? DriverId,
            driverName: driverName ?? DriverName,
            vehicleId: vehicleId ?? VehicleId,
            vehicleUnit: vehicleUnit ?? VehicleUnit,
            seatsCapacity: seatsCapacity,
            seatsMinimum: null);

    /// <summary>Deadhead return with the baseline assignment driving the unit back.</summary>
    public static Result<Trip> DeadheadReturn(this Trip source, string tripNumber) =>
        source.CreateDeadheadReturn(tripNumber, DriverId, DriverName, VehicleId, VehicleUnit);
}
