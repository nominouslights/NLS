using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Alerts;

/// <summary>
/// The Dispatch Board's "Compliance alerts" tile and the Compliance screen's today list:
/// everything about the roster's hours and compliance standing that someone should look at
/// right now, computed from what this module owns — the roster batch evaluation (band Soon
/// → near a limit, Over → over one), active out-of-service declarations, open ELD
/// malfunctions, yesterday's uncertified records and exceptions still awaiting
/// acknowledgement. Trips at risk from an unfit assigned driver are NOT here: Trips owns the
/// assignment and the fitness verdict, so that figure is its to serve.
/// </summary>
public sealed record GetHosAlertsQuery(Guid TenantId) : IQuery<HosAlertsResponse>;

/// <summary>
/// Body of <c>GET /api/drivers/hos/alerts/today</c>. The six counts summarise
/// <paramref name="Items"/> — one item per driver for the hours bands and out-of-service, one
/// per open malfunction, uncertified day and declared exception — so a tile can show the
/// numbers and a list can show the rows from one call. <paramref name="Items"/> are ordered
/// most urgent first (<see cref="HosAlertKind"/> order), then by driver name. Every figure is
/// a planning figure (<paramref name="Label"/>): the record of duty status is on the bus's ELD.
/// </summary>
public sealed record HosAlertsResponse(
    DateTimeOffset AsOfUtc,
    int DriversNearLimit,
    int DriversOverLimit,
    int OutOfService,
    int OpenMalfunctions,
    int UncertifiedYesterday,
    int UnassignedExceptions,
    IReadOnlyList<HosAlertItemResponse> Items,
    string Label);

/// <summary>One row of the alerts list. <paramref name="Kind"/> is a <see cref="HosAlertKind"/> member name.</summary>
public sealed record HosAlertItemResponse(
    Guid DriverId,
    string DriverName,
    string Kind,
    string Message);

/// <summary>
/// The alert kinds, in the order the list shows them — the ones that stop a driver first, then
/// the ones that need a signature or an acknowledgement. Member names travel on the wire verbatim.
/// </summary>
public enum HosAlertKind
{
    /// <summary>An out-of-service declaration is in force right now.</summary>
    OutOfService,

    /// <summary>The engine's band is Over: a daily, shift, cycle or rest limit is exhausted.</summary>
    OverLimit,

    /// <summary>The engine's band is Soon: the tightest remaining figure is inside the "soon" band.</summary>
    NearLimit,

    /// <summary>An ELD malfunction report is open — the driver is on paper logs.</summary>
    EldMalfunction,

    /// <summary>An adverse-conditions or emergency declaration nobody has acknowledged yet.</summary>
    ExceptionDeclared,

    /// <summary>Yesterday's record exists and the driver has not certified it.</summary>
    Uncertified,
}
