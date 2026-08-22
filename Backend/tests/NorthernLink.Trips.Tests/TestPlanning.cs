using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Routes;
using NorthernLink.Trips.Domain.Schedules;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Tests;

/// <summary>Factory helpers for the trip-planning aggregates with valid baseline payloads.</summary>
internal static class TestPlanning
{
    public static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

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
        int generationHorizonDays = 7,
        bool active = true,
        Guid? routeId = null,
        ScheduleRecurrenceKind recurrenceKind = ScheduleRecurrenceKind.DaysOfWeek,
        int? intervalDays = null,
        DateOnly? anchorDate = null,
        IReadOnlyList<int>? daysOfMonth = null)
    {
        var result = CreateTemplateResult(
            daysOfWeek, departureTime, returnDepartureTime, generationHorizonDays,
            routeId, recurrenceKind, intervalDays, anchorDate, daysOfMonth);
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
        int generationHorizonDays = 7,
        Guid? routeId = null,
        ScheduleRecurrenceKind recurrenceKind = ScheduleRecurrenceKind.DaysOfWeek,
        int? intervalDays = null,
        DateOnly? anchorDate = null,
        IReadOnlyList<int>? daysOfMonth = null) =>
        ScheduleTemplate.Create(
            TenantId,
            "Alamos crew shuttle",
            routeId ?? Guid.NewGuid(),
            TripServiceType.ContractCrew,
            clientId: null,
            clientName: "Alamos Gold",
            recurrenceKind: recurrenceKind,
            daysOfWeek: daysOfWeek ?? [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            intervalDays: intervalDays,
            anchorDate: anchorDate,
            daysOfMonth: daysOfMonth ?? [],
            departureTime: departureTime ?? new TimeOnly(6, 30),
            returnDepartureTime: returnDepartureTime,
            seatsCapacity: 12,
            seatsMinimum: null,
            defaultVehicleUnit: "U-04",
            defaultDriverId: null,
            generationHorizonDays: generationHorizonDays);

    /// <summary>A valid scheduled trip; override the arguments a test cares about.</summary>
    public static Result<Trip> ScheduleTrip(
        string tripNumber = "TR-1001",
        Guid? driverId = null,
        string? driverName = null,
        Guid? vehicleId = null,
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
        bool isEmptyLeg = false) =>
        Trip.Schedule(
            tenantId ?? TenantId,
            tripNumber,
            serviceDate: serviceDate ?? new DateOnly(2026, 7, 21),
            windowStart: windowStart ?? new TimeOnly(6, 30),
            windowEnd: new TimeOnly(8, 15),
            TripServiceType.ContractCrew,
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
            driverId: driverId,
            driverName: driverName,
            vehicleId: vehicleId,
            vehicleUnit: "U-04",
            seatsCapacity: seatsCapacity,
            seatsMinimum: null);
}
