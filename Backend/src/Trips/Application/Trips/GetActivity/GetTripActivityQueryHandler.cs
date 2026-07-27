using System.Text.Json;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Trips;

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
    ITripActivityReadService activity)
    : IQueryHandler<GetTripActivityQuery, IReadOnlyList<TripActivityEntryResponse>>
{
    /// <summary>
    /// Stable journal aggregate name for the manifest (<c>AuditNames.ForAggregate(typeof(TripManifest))</c>).
    /// Only rows of this aggregate type carry <c>source</c>/<c>enteredBy</c> provenance.
    /// </summary>
    private const string ManifestAggregateType = "trip-manifest";

    public async Task<Result<IReadOnlyList<TripActivityEntryResponse>>> Handle(
        GetTripActivityQuery query, CancellationToken cancellationToken)
    {
        var trip = await trips.GetByIdAsync(query.TripId, cancellationToken);
        if (trip is null)
        {
            return Result.Failure<IReadOnlyList<TripActivityEntryResponse>>(TripErrors.NotFound);
        }

        var entries = await activity.GetJournalEntriesAsync(query.TripId, trip.ManifestId, cancellationToken);

        IReadOnlyList<TripActivityEntryResponse> timeline = entries
            .OrderBy(e => e.OccurredAtUtc)
            .Select(Map)
            .ToList();

        return Result.Success(timeline);
    }

    private static TripActivityEntryResponse Map(TripActivityJournalEntry entry)
    {
        var (source, enteredBy) = entry.AggregateType == ManifestAggregateType
            ? ReadManifestProvenance(entry.Payload)
            : (null, null);

        return new TripActivityEntryResponse(
            entry.OccurredAtUtc, entry.AggregateType, entry.EventType, source, enteredBy);
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
