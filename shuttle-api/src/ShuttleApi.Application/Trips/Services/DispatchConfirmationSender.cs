using Microsoft.Extensions.Logging;
using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips.Services;

internal sealed class DispatchConfirmationSender(
    IClientRepository clientRepository,
    IClientEmailTemplateRepository templateRepository,
    IEmailTemplateRenderer renderer,
    INotificationService notificationService,
    ITripRepository tripRepository,
    ILogger<DispatchConfirmationSender> logger)
    : IDispatchConfirmationSender
{
    public async Task SendAllAsync(Trip trip, CancellationToken cancellationToken)
    {
        if (trip.ClientId is null)
            return;

        var client = await clientRepository.GetByIdAsync(trip.ClientId.Value, cancellationToken);
        if (client is null)
        {
            logger.LogWarning("Client {ClientId} not found for trip {TripId}; skipping confirmation emails.", trip.ClientId, trip.Id);
            return;
        }

        var anyEmailSent = false;

        foreach (var passenger in trip.Passengers)
        {
            var email = !string.IsNullOrWhiteSpace(passenger.Email)
                ? passenger.Email
                : passenger.ContactInfo;

            if (string.IsNullOrWhiteSpace(email))
                continue;

            var direction = passenger.Direction ?? "Outbound";
            var templateType = direction.Equals("Inbound", StringComparison.OrdinalIgnoreCase)
                ? ClientEmailTemplateType.InboundConfirmation
                : ClientEmailTemplateType.OutboundConfirmation;

            var template = await templateRepository.GetByClientAndTypeAsync(trip.ClientId.Value, templateType, cancellationToken);
            if (template is null)
            {
                logger.LogWarning(
                    "No {TemplateType} template configured for client {ClientId}; skipping confirmation for passenger {PassengerName}.",
                    templateType, trip.ClientId, passenger.Name);
                continue;
            }

            var context = new EmailTemplateContext
            {
                Trip = trip,
                Client = client,
                Passenger = passenger
            };

            var subject = renderer.Render(template.Subject, context);
            var body = renderer.Render(template.Body, context);

            try
            {
                await notificationService.SendEmailAsync(email, subject, body, cancellationToken);
                passenger.RecordEmailSent(email, direction, isTest: false);
                anyEmailSent = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to send {TemplateType} confirmation to {Email} for passenger {PassengerName} on trip {TripId}.",
                    templateType, email, passenger.Name, trip.Id);
            }
        }

        if (anyEmailSent)
            await tripRepository.SaveChangesAsync(cancellationToken);
    }
}
