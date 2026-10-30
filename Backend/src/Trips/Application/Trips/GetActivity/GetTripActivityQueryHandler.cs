using System.Text.Json;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Trips;
using NorthernLink.Trips.Domain.Trips.Events;

namespace NorthernLink.Trips.Application.Trips.GetActivity;

/// <summary>
/// Builds a trip's activity timeline from the append-only <c>event_journal</c>: loads the
/// trip to discover its attached manifest, unions the trip's and manifest's journal rows,
/// orders oldest-first, and mines <see cref="TripActivityEntryResponse.Source"/> /
/// <see cref="TripActivityEntryResponse.EnteredBy"/> out of manifest event payloads. Read
/// only — it never writes the journal.
/// </summary>
public sealed class GetTripActivityQueryHandler(
    ITripRepository trips,
    ITripActivityReadService activity,
    TripOperatorAccess operatorAccess)
    : IQueryHandler<GetTripActivityQuery, IReadOnlyList<TripActivityEntryResponse>>
{
    /// <summary>
    /// Stable journal aggregate name for the manifest ("trip-manifest"). Only rows of this
    /// aggregate type carry <c>source</c>/<c>enteredBy</c> provenance. Derived, never a literal,
    /// so it cannot drift from what <c>ModuleDbContext</c> writes; tests pin the literal value.
    /// </summary>
    private static readonly string ManifestAggregateType = AuditNames.ForAggregate(typeof(TripManifest));

    /// <summary>Stable journal aggregate name for the trip ("trip").</summary>
    private static readonly string TripAggregateType = AuditNames.ForAggregate(typeof(Trip));

    /// <summary>Journal event name of <see cref="TripDriverCorrectedDomainEvent"/> ("trip-driver-corrected").</summary>
    private static readonly string DriverCorrectedEventType = AuditNames.ForEvent(typeof(TripDriverCorrectedDomainEvent));

    public async Task<Result<IReadOnlyList<TripActivityEntryResponse>>> Handle(
        GetTripActivityQuery query, CancellationToken cancellationToken)
    {
        var trip = await trips.GetByIdAsync(query.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure<IReadOnlyList<TripActivityEntryResponse>>(TripErrors.NotFound);
        }

        // The identity gate: the activity route is on the DriverAccess group, and a journal
        // names the dispatchers and manifests behind a run — only its own driver may read it.
        if (!await operatorAccess.MayOperateAsync(trip.DriverId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<TripActivityEntryResponse>>(TripErrors.NotYourTrip);
        }

        var entries = await activity.GetJournalEntriesAsync(query.TripId, trip.ManifestId, cancellationToken);

        // Past the gate, a caller without a dispatch role is here only as the trip's own driver.
        // They see that a trip event happened (e.g. "Driver corrected") but not the dispatch-side
        // narrative behind it: the free-text reason, the before/after detail, and who made it.
        var redactTripNarrative = !operatorAccess.HasDispatchAccess;

        IReadOnlyList<TripActivityEntryResponse> timeline = entries
            .OrderBy(e => e.OccurredAtUtc)
            .Select(e => Map(e, redactTripNarrative))
            .ToList();

        return Result.Success(timeline);
    }

    private static TripActivityEntryResponse Map(TripActivityJournalEntry entry, bool redactTripNarrative)
    {
        if (entry.AggregateType == ManifestAggregateType)
        {
            var (source, manifestEnteredBy) = ReadManifestProvenance(entry.Payload);
            return new TripActivityEntryResponse(
                entry.OccurredAtUtc, entry.AggregateType, entry.EventType, source, manifestEnteredBy);
        }

        if (entry.AggregateType != TripAggregateType)
        {
            return new TripActivityEntryResponse(
                entry.OccurredAtUtc, entry.AggregateType, entry.EventType, null, null);
        }

        // Trip events journal their whole domain-event record, so anything that lives only on
        // the event — a reason, who made the change, the before/after of a correction — is read
        // straight back out. Generic on purpose: any trip event carrying "reason" or "enteredBy"
        // surfaces without a change here — which is also why the driver-only redaction is applied
        // to every trip event, not just the correction.
        if (redactTripNarrative)
        {
            return new TripActivityEntryResponse(
                entry.OccurredAtUtc, entry.AggregateType, entry.EventType, null, null, null, null);
        }

        var (reason, enteredBy, detail) = ReadTripPayload(entry.EventType, entry.Payload);
        return new TripActivityEntryResponse(
            entry.OccurredAtUtc, entry.AggregateType, entry.EventType, null, enteredBy, reason, detail);
    }

    /// <summary>
    /// The <c>reason</c> and <c>enteredBy</c> string fields of a trip event payload, plus the
    /// event-specific <see cref="TripActivityEntryResponse.Detail"/> — each null when absent,
    /// non-string, or the JSON is malformed.
    /// </summary>
    private static (string? Reason, string? EnteredBy, string? Detail) ReadTripPayload(string eventType, string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (null, null, null);
            }

            var detail = eventType == DriverCorrectedEventType ? DriverCorrectionDetail(root) : null;
            return (ReadString(root, "reason"), ReadString(root, "enteredBy"), detail);
        }
        catch (JsonException)
        {
            return (null, null, null);
        }
    }

    /// <summary>"Driver: R. Ballantyne → J. Moose" — "none" when the trip had no driver on record.</summary>
    private static string? DriverCorrectionDetail(JsonElement root)
    {
        var newDriver = ReadString(root, "newDriverName");
        if (newDriver is null)
        {
            return null;
        }

        var previousDriver = ReadString(root, "previousDriverName");
        return $"Driver: {previousDriver ?? "none"} → {newDriver}";
    }

    /// <summary>
    /// Defensively pulls <c>source</c> and <c>enteredBy</c> out of a manifest event payload
    /// (camelCase, enums-as-strings — see <c>AuditJson</c>). Missing on the manifest-created
    /// event and on any older/renamed payload shape; a missing or non-string field, or malformed
    /// JSON, yields null rather than throwing.
    /// </summary>
    private static (string? Source, string? EnteredBy) ReadManifestProvenance(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (null, null);
            }

            return (ReadString(root, "source"), ReadString(root, "enteredBy"));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string? ReadString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
