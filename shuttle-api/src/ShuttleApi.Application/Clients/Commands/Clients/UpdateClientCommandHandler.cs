using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Application.Clients;

internal sealed class UpdateClientCommandHandler(IClientRepository clientRepository)
    : IRequestHandler<UpdateClientCommand>
{
    public async Task Handle(UpdateClientCommand request, CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Client {request.Id} not found.");

        client.Update(
            request.BusinessName,
            request.ServiceType,
            request.PrimaryContactName,
            request.PrimaryContactTitle,
            request.Phone,
            request.Email,
            request.StreetAddress,
            request.City,
            request.Province,
            request.PostalCode,
            request.GstHstNumber,
            request.PreferredPaymentMethod,
            request.NetPaymentTerms,
            request.ComplianceNotes,
            request.IsMinesite,
            request.Industry,
            request.ProjectSite);

        if (request.IsActive) client.Activate(); else client.Deactivate();

        if (request.NotificationEmails is not null)
        {
            client.SetNotificationEmails(
                ClientNotificationCategory.Notifications,
                request.NotificationEmails);
        }

        if (request.TripDepartureArrivalEmails is not null)
        {
            client.SetNotificationEmails(
                ClientNotificationCategory.TripDepartureArrival,
                request.TripDepartureArrivalEmails);
        }

        if (request.PassengerBookingEmails is not null)
        {
            client.SetNotificationEmails(
                ClientNotificationCategory.PassengerBooking,
                request.PassengerBookingEmails);
        }

        await clientRepository.UpdateAsync(client, cancellationToken);
    }
}
