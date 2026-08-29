namespace NorthernLink.Trips.Application.Trips;

/// <summary>
/// Public contract for a trip (list and detail — same shape). Enum-typed fields travel
/// as their string names; "open — needs coverage" and "empty leg" chips are frontend
/// derivations from <see cref="Status"/> + <see cref="DriverId"/> / <see cref="IsEmptyLeg"/>.
/// <para>
/// Two timestamps, deliberately: <see cref="OperationsFinishedAtUtc"/> is when the run ended and
/// <see cref="CompletedAtUtc"/> is when the money arrived. For a client trip they are days or
/// weeks apart — anything showing "when did this trip finish" wants the former.
/// </para>
/// </summary>
public sealed record TripResponse(
    Guid Id,
    string TripNumber,
    DateOnly ServiceDate,
    TimeOnly WindowStart,
    TimeOnly? WindowEnd,
    string ServiceType,
    Guid? RouteId,
    string RouteName,
    string Origin,
    string Destination,
    IReadOnlyList<TripStopResponse> Stops,
    int DistanceKm,
    Guid? ScheduleTemplateId,
    string? RoundTripKey,
    string? Direction,
    bool IsEmptyLeg,
    Guid? ClientId,
    string? ClientName,
    string? PoNumber,
    Guid? DriverId,
    string? DriverName,
    Guid? VehicleId,
    string? VehicleUnit,
    int? SeatsCapacity,
    int SeatsConfirmed,
    int? SeatsMinimum,
    bool DemandGuaranteed,
    string Status,
    Guid? ManifestId,
    bool HasPostTripInspection,
    DateTimeOffset? OperationsFinishedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? CancelledReason,
    string? WrittenOffReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    TripBillingResponse? Billing);

/// <summary>
/// A trip's billing state, replicated from Billing (never joined across modules). Null when no
/// worksheet claims the trip.
/// <para>
/// This is worksheet <em>detail</em> — which invoice, which QBO number, which dates. Whether the
/// trip is ready to be billed, invoiced, or settled is no longer read from here: it is the trip's
/// own <see cref="TripResponse.Status"/>, driven by these same events.
/// </para>
/// <c>State</c> is the wire value from <c>TripBillingStates</c>: OnWorksheet, Invoiced, Paid, or
/// WrittenOff.
/// </summary>
public sealed record TripBillingResponse(
    string State,
    Guid InvoiceId,
    string InvoiceNumber,
    string? QboInvoiceId,
    DateOnly? QboEnteredDate,
    DateOnly? PaymentConfirmedDate);

/// <summary>
/// One ordered stop on a trip's route snapshot. <see cref="StopId"/> and the coordinates come
/// from the catalog stop the route was built from and are null for legacy free-text stops.
/// The two offsets are the stop's timetable entry (minutes after the leg's departure); the
/// caller picks the one matching the trip's <c>Direction</c> — <see cref="ReturnOffsetMinutes"/>
/// for an Inbound leg, <see cref="OutboundOffsetMinutes"/> otherwise — and adds it to
/// <c>WindowStart</c> to get the time this trip reaches this stop.
/// </summary>
public sealed record TripStopResponse(
    string Name,
    int Order,
    Guid? StopId,
    double? Latitude,
    double? Longitude,
    int? OutboundOffsetMinutes,
    int? ReturnOffsetMinutes);
