using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.MergeRoundTrip;

/// <summary>
/// Merges an existing outbound and inbound client trip into one round trip
/// (POST /api/trips/{id}/merge-round-trip). Validation is hard — same client, same
/// service date, mirrored corridors, neither Cancelled, neither already paired — and the
/// chronologically earlier leg (service date, then window start) becomes the Outbound leg
/// (ties go to <see cref="TripId"/>). <see cref="AllowMismatch"/> is the dispatcher's
/// manual override: it relaxes only the same-service-date and mirrored-corridor checks
/// (overnight returns, reworded corridors); client/tenant/Cancelled/already-paired stay hard.
/// <para>
/// <see cref="Reason"/> is optional when both legs are still open (Scheduled/InProgress) and
/// <b>required</b> when either leg is operationally closed — a ReadyForBilling, Invoiced or
/// Completed leg is work that has already been reported or billed, so re-keying it has to leave
/// a documented "why" behind. Trimmed, 500 characters maximum. The handler enforces this;
/// the aggregate deliberately does not (see <c>TripErrors.RoundTripReasonRequired</c>).
/// </para>
/// </summary>
public sealed record MergeRoundTripCommand(
    Guid TripId,
    Guid OtherTripId,
    bool AllowMismatch = false,
    string? Reason = null) : ICommand;
