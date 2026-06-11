using System.Runtime.InteropServices;
using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Common.Services;

internal sealed class EmailTemplateRenderer : IEmailTemplateRenderer
{
    public string Render(string template, EmailTemplateContext context)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        var trip = context.Trip;
        var localDeparture = ToLocal(trip.ScheduledAt);
        var localArrive = ToLocal(trip.ScheduledAt.AddMinutes(-30));

        var firstStop = trip.Stops.OrderBy(s => s.SequenceOrder).FirstOrDefault();
        var lastStop = trip.Stops.OrderBy(s => s.SequenceOrder).LastOrDefault();

        var pickup = FormatStop(firstStop);
        var destination = FormatStop(lastStop);
        var route = firstStop is not null && lastStop is not null && firstStop != lastStop
            ? $"{firstStop.LocationName} \u2192 {lastStop.LocationName}"
            : firstStop?.LocationName ?? string.Empty;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TripDate"] = localDeparture.ToString("MMMM d, yyyy"),
            ["DepartureTime"] = localDeparture.ToString("h:mm tt"),
            ["ArrivalTime"] = localArrive.ToString("h:mm tt"),
            ["PickupLocation"] = pickup,
            ["Destination"] = destination,
            ["EstimatedArrival"] = string.Empty,
            ["Route"] = route,
            ["Status"] = context.Status ?? "On Time",
            ["StopLocation"] = context.StopLocation ?? lastStop?.LocationName ?? string.Empty,
            ["PassengerName"] = context.Passenger?.Name ?? string.Empty,
            ["PassengerNames"] = FormatPassengerNames(trip),
            ["ClientName"] = context.Client?.BusinessName ?? string.Empty
        };

        var result = template;
        foreach (var (key, value) in values)
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);

        return result;
    }

    private static string FormatStop(TripStop? stop)
    {
        if (stop is null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(stop.Address)
            ? stop.LocationName
            : $"{stop.LocationName}, {stop.Address}";
    }

    private static string FormatPassengerNames(Trip trip)
    {
        var names = trip.Passengers
            .Where(p => p.PaymentStatus != PassengerPaymentStatus.Cancelled
                && p.PaymentStatus != PassengerPaymentStatus.Released)
            .Select(p => p.Name)
            .ToList();

        return names.Count == 0
            ? string.Empty
            : string.Join("\n", names.Select(n => $"\u2022 {n}"));
    }

    private static DateTime ToLocal(DateTime utc)
    {
        var tzId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Central Standard Time"
            : "America/Chicago";

        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utc, DateTimeKind.Utc),
                TimeZoneInfo.FindSystemTimeZoneById(tzId));
        }
        catch (TimeZoneNotFoundException)
        {
            return utc;
        }
    }
}
