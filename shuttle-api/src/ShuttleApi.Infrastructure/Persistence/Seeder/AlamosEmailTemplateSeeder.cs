using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Infrastructure.Persistence.Seeder;

public static class AlamosEmailTemplateSeeder
{
    private const string Signature =
        "\n\nEmelio Campbell\nNorthern Link Shuttle and Cargo\n(204) 441-7724\nemelio.campbell@northernlinkshuttleandcargo.com";

    private static readonly (ClientEmailTemplateType Type, string Subject, string Body)[] Templates =
    [
        (
            ClientEmailTemplateType.OutboundConfirmation,
            "Shuttle Confirmation \u2014 {{TripDate}}",
            "This email confirms your shuttle transportation to the Alamos Gold Lynn Lake Project on {{TripDate}}.\n\n" +
            "DEPARTURE DETAILS\n" +
            "\u2022 Pickup Location: {{PickupLocation}} (lobby)\n" +
            "\u2022 Arrival Time: Please arrive by {{ArrivalTime}}\n" +
            "\u2022 Departure Time: {{DepartureTime}} sharp\n" +
            "\u2022 Destination: {{Destination}}\n" +
            "\u2022 Estimated Arrival: depending on road conditions\n\n" +
            "CHECK-IN PROCEDURE\n" +
            "Upon arrival, please check in at the hotel front desk and inform them you are waiting for the Northern Link Shuttle. " +
            "The shuttle will depart promptly at {{DepartureTime}} from the front entrance.\n\n" +
            "PARKING\n" +
            "Secured, fenced parking with block heater plug-ins is available in Thompson for the duration of your rotation. " +
            "Please discuss this need with your supervisor and reply directly to this email if parking is required." +
            Signature
        ),
        (
            ClientEmailTemplateType.InboundConfirmation,
            "Return Shuttle Confirmation \u2014 {{TripDate}}",
            "This email confirms your return shuttle transportation from the Alamos Gold Lynn Lake Project on {{TripDate}}.\n\n" +
            "DEPARTURE DETAILS\n" +
            "\u2022 Pickup Location: {{PickupLocation}}\n" +
            "\u2022 Arrival Time: Please be ready by {{ArrivalTime}}\n" +
            "\u2022 Departure Time: {{DepartureTime}} sharp\n" +
            "\u2022 Destination: {{Destination}}\n" +
            "\u2022 Estimated Arrival: depending on road conditions\n\n" +
            "CHECK-IN PROCEDURE\n" +
            "Please be at the pickup point ahead of departure. The shuttle will depart promptly at {{DepartureTime}}.\n\n" +
            "If you have any questions, reply directly to this email." +
            Signature
        ),
        (
            ClientEmailTemplateType.DepartureNotification,
            "Shuttle Departure \u2014 {{TripDate}}",
            "Confirming departure for the {{TripDate}} shuttle.\n\n" +
            "DEPARTURE CONFIRMATION:\n" +
            "Route: {{Route}}\n" +
            "Departure Time: {{DepartureTime}}\n\n" +
            "Status: {{Status}}\n\n" +
            "Passengers onboard:\n{{PassengerNames}}" +
            Signature
        ),
        (
            ClientEmailTemplateType.ArrivalNotification,
            "Shuttle Arrival \u2014 {{TripDate}}",
            "Confirming arrival for the {{TripDate}} shuttle.\n\n" +
            "ARRIVAL CONFIRMATION:\n" +
            "Route: {{Route}}\n\n" +
            "Status: {{Status}}\n\n" +
            "Passengers onboard:\n{{PassengerNames}}" +
            Signature
        ),
        (
            ClientEmailTemplateType.StopUpdate,
            "Shuttle Update \u2014 {{TripDate}}",
            "Update for the {{TripDate}} shuttle.\n\n" +
            "Route: {{Route}}\n" +
            "Current Stop: {{StopLocation}}\n\n" +
            "Status: {{Status}}\n\n" +
            "Passengers onboard:\n{{PassengerNames}}" +
            Signature
        )
    ];

    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        var alamos = await db.Clients
            .FirstOrDefaultAsync(c => EF.Functions.ILike(c.BusinessName, "%alamos%"));

        if (alamos is null)
            return;

        var existingTypes = await db.ClientEmailTemplates
            .Where(t => t.ClientId == alamos.Id)
            .Select(t => t.Type)
            .ToListAsync();

        var added = 0;
        foreach (var (type, subject, body) in Templates)
        {
            if (existingTypes.Contains(type))
                continue;

            await db.ClientEmailTemplates.AddAsync(
                ClientEmailTemplate.Create(alamos.Id, type, subject, body));
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Seed: {Count} Alamos email templates created.", added);
        }
    }
}
