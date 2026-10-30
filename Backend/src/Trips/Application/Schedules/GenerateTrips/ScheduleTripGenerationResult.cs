namespace NorthernLink.Trips.Application.Schedules.GenerateTrips;

/// <summary>
/// What one generation run over <c>[From, Through]</c> (both inclusive) did — or, from the
/// preview query, would do. <see cref="TripCount"/> is the number of trips created (generate)
/// or that would be created (preview); <see cref="AlreadyExisted"/> is how many occurrences
/// in the window were already materialized and skipped. <see cref="Outbound"/>/<see cref="Inbound"/>
/// split <see cref="TripCount"/> by leg; the first/last service dates are those of the new
/// trips only, null when there are none. Serialized camelCase with <c>yyyy-MM-dd</c> dates —
/// the shape the Dispatcher's Generate Trips dialog is built against.
/// </summary>
public sealed record ScheduleTripGenerationResult(
    DateOnly From,
    DateOnly Through,
    int TripCount,
    int AlreadyExisted,
    int Outbound,
    int Inbound,
    DateOnly? FirstServiceDate,
    DateOnly? LastServiceDate);
