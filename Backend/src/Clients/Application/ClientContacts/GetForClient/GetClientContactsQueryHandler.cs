using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;

namespace NorthernLink.Clients.Application.ClientContacts.GetForClient;

public sealed class GetClientContactsQueryHandler(IClientContactReadService readService)
    : IQueryHandler<GetClientContactsQuery, IReadOnlyList<ClientContactResponse>>
{
    public async Task<Result<IReadOnlyList<ClientContactResponse>>> Handle(
        GetClientContactsQuery query, CancellationToken cancellationToken)
    {
        var contacts = await readService.GetForClientAsync(query.ClientId, cancellationToken);
        return Result.Success(contacts);
    }
}
