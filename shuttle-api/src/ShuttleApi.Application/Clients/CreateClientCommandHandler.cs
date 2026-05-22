using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Application.Clients;

internal sealed class CreateClientCommandHandler(IClientRepository clientRepository)
    : IRequestHandler<CreateClientCommand, CreateClientResult>
{
    public async Task<CreateClientResult> Handle(CreateClientCommand request, CancellationToken cancellationToken)
    {
        var client = Client.Create(
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
            request.IsMinesite);

        await clientRepository.AddAsync(client, cancellationToken);

        return new CreateClientResult(client.Id);
    }
}
