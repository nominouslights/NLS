using NorthernLink.Shared.Messaging;

namespace NorthernLink.Booking.Application.Settings.GetSettings;

/// <summary>
/// The whole settings surface in one read: the tenant policy (effective defaults when no
/// row has been written yet — GET never 404s) plus every corridor override row, joined to
/// the corridor replica for display names. Readable by dispatch (calendar math needs the
/// thresholds); only writes are admin-gated.
/// </summary>
public sealed record GetBookingSettingsQuery(Guid TenantId) : IQuery<BookingSettingsResponse>;

/// <summary>Policy + per-corridor overrides.</summary>
public sealed record BookingSettingsResponse(
    BookingPolicyResponse Policy,
    IReadOnlyList<CorridorSettingsResponse> Corridors);

/// <summary>
/// The tenant policy values. <c>IsPersisted</c> is false while the tenant is still on the
/// built-in defaults (no policy row written yet).
/// </summary>
public sealed record BookingPolicyResponse(
    int CancellationWindowHours,
    decimal EarlyCancellationPenaltyCad,
    int BookingCutoffHours,
    int SeatHoldMinutes,
    int DefaultPassengerMinimum,
    int DefaultSeatCapacity,
    bool IsPersisted);

/// <summary>
/// One corridor's override row. CorridorName falls back to the corridor id string if the
/// replica has no row for it (route not yet re-saved).
/// </summary>
public sealed record CorridorSettingsResponse(
    Guid CorridorId,
    string CorridorName,
    int? PassengerMinimum,
    int? SeatCapacity);
