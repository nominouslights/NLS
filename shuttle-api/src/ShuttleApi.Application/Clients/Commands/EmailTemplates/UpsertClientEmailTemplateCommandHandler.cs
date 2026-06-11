using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Common;

namespace ShuttleApi.Application.Clients;

internal sealed class UpsertClientEmailTemplateCommandHandler(
    IClientRepository clientRepository,
    IClientEmailTemplateRepository templateRepository)
    : IRequestHandler<UpsertClientEmailTemplateCommand>
{
    public async Task Handle(UpsertClientEmailTemplateCommand request, CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByIdAsync(request.ClientId, cancellationToken)
            ?? throw new NotFoundException($"Client {request.ClientId} not found.");

        var existing = await templateRepository.GetByClientAndTypeAsync(
            client.Id, request.Type, cancellationToken);

        if (existing is null)
        {
            var template = ClientEmailTemplate.Create(
                client.Id, request.Type, request.Subject, request.Body);
            await templateRepository.AddAsync(template, cancellationToken);
        }
        else
        {
            existing.Update(request.Subject, request.Body);
            await templateRepository.UpdateAsync(existing, cancellationToken);
        }
    }
}
