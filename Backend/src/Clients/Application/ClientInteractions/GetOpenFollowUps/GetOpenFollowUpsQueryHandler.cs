using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Clients.Application.Abstractions;

namespace NorthernLink.Clients.Application.ClientInteractions.GetOpenFollowUps;

public sealed class GetOpenFollowUpsQueryHandler(IClientInteractionReadService readService)
    : IQueryHandler<GetOpenFollowUpsQuery, IReadOnlyList<ClientInteractionResponse>>
{
    public async Task<Result<IReadOnlyList<ClientInteractionResponse>>> Handle(
        GetOpenFollowUpsQuery query,
        CancellationToken cancellationToken)
    {
        var followUps = await readService.GetOpenFollowUpsAsync(query.TenantId, cancellationToken);
        return Result.Success(followUps);
    }
}
