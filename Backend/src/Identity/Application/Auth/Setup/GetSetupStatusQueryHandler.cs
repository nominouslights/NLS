using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.Setup;

public sealed class GetSetupStatusQueryHandler(IUserRepository userRepository)
    : IQueryHandler<GetSetupStatusQuery, SetupStatusResponse>
{
    public async Task<Result<SetupStatusResponse>> Handle(GetSetupStatusQuery query, CancellationToken cancellationToken)
    {
        var anyUser = await userRepository.AnyAsync(cancellationToken);
        return Result.Success(new SetupStatusResponse(SetupRequired: !anyUser));
    }
}
