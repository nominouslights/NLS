using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Application.Clients;

internal sealed class GetClientEmailTemplatesQueryHandler(
    IClientEmailTemplateRepository templateRepository)
    : IRequestHandler<GetClientEmailTemplatesQuery, IReadOnlyList<ClientEmailTemplateResult>>
{
    public async Task<IReadOnlyList<ClientEmailTemplateResult>> Handle(
        GetClientEmailTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await templateRepository.GetByClientIdAsync(request.ClientId, cancellationToken);

        return templates
            .Select(t => new ClientEmailTemplateResult(t.Id, t.Type, t.Subject, t.Body, t.UpdatedAt))
            .ToList();
    }
}
