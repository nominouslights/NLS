using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

internal sealed class SendPassengerConfirmationCommandHandler(
    ITripRepository tripRepository,
    IClientRepository clientRepository,
    IClientEmailTemplateRepository templateRepository,
    IEmailTemplateRenderer renderer,
    INotificationService notificationService)
    : IRequestHandler<SendPassengerConfirmationCommand>
{
    public async Task Handle(SendPassengerConfirmationCommand request, CancellationToken cancellationToken)
    {
        var trip = await tripRepository.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new NotFoundException($"Trip {request.TripId} not found.");

        var passenger = trip.Passengers.FirstOrDefault(p => p.Id == request.PassengerId)
            ?? throw new NotFoundException($"Passenger {request.PassengerId} not found on this trip.");

        var recipient = !string.IsNullOrWhiteSpace(passenger.Email)
            ? passenger.Email
            : passenger.ContactInfo;

        if (string.IsNullOrWhiteSpace(recipient))
            throw new ArgumentException("This passenger has no email address on file.");

        if (trip.ClientId is null)
            throw new ArgumentException("Confirmation emails are only available for charter trips with a client.");

        var type = request.Direction.Equals("Inbound", StringComparison.OrdinalIgnoreCase)
            ? ClientEmailTemplateType.InboundConfirmation
            : ClientEmailTemplateType.OutboundConfirmation;

        var client = await clientRepository.GetByIdAsync(trip.ClientId.Value, cancellationToken);

        var template = await templateRepository.GetByClientAndTypeAsync(trip.ClientId.Value, type, cancellationToken)
            ?? throw new ArgumentException($"No {type} email template is configured for this client.");

        var context = new EmailTemplateContext
        {
            Trip = trip,
            Client = client,
            Passenger = passenger
        };

        var subject = renderer.Render(template.Subject, context);
        var body = renderer.Render(template.Body, context);

        await notificationService.SendEmailAsync(recipient!, subject, body, cancellationToken);
    }
}
