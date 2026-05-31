using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Users;

namespace ShuttleApi.Application.Setup;

internal sealed class GetSetupStatusQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetSetupStatusQuery, SetupStatusResult>
{
    public async Task<SetupStatusResult> Handle(GetSetupStatusQuery request, CancellationToken cancellationToken)
    {
        var adminExists = await userRepository.AnyAdminExistsAsync(cancellationToken);
        return new SetupStatusResult(adminExists);
    }
}
