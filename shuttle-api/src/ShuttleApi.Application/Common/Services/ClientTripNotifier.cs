using Microsoft.Extensions.Logging;
using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Common.Services;

internal sealed class ClientTripNotifier(
    IClientRepository clientRepository,
    IClientEmailTemplateRepository templateRepository,
    IEmailTemplateRenderer renderer,
    INotificationService notificationService,
    ILogger<ClientTripNotifier> logger)
    : IClientTripNotifier
{
    public async Task NotifyDepartureArrivalAsync(
        Trip trip,
        ClientEmailTemplateType type,
        string status,
        string? stopLocation = null,
        CancellationToken cancellationToken = default)
    {
        if (trip.ServiceType != TripServiceType.Charter || trip.ClientId is null)
            return;

        var client = await clientRepository.GetByIdAsync(trip.ClientId.Value, cancellationToken);
        if (client is null)
            return;

        var recipients = client.NotificationEmails
            .Where(e => e.Category == ClientNotificationCategory.TripDepartureArrival)
            .Select(e => e.Email)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
            return;

        var template = await templateRepository.GetByClientAndTypeAsync(client.Id, type, cancellationToken);
        if (template is null)
        {
            logger.LogWarning(
                "No {Type} email template configured for client {ClientId}; skipping notification.",
                type, client.Id);
            return;
        }

        var context = new EmailTemplateContext
        {
            Trip = trip,
            Client = client,
            Status = status,
            StopLocation = stopLocation
        };

        var subject = renderer.Render(template.Subject, context);
        var body = renderer.Render(template.Body, context);

        foreach (var recipient in recipients)
        {
            try
            {
                await notificationService.SendEmailAsync(recipient, subject, body, cancellationToken);
            }
            catch (Exception ex)
            {
                // Best-effort: a failed email must not roll back the trip status change.
                logger.LogError(ex,
                    "Failed to send {Type} email to {Recipient} for trip {TripId}.",
                    type, recipient, trip.Id);
            }
        }
    }
}
