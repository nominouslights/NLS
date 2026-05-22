using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Domain.Users;

namespace ShuttleApi.Application.Users;

internal sealed class GetPendingUsersQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetPendingUsersQuery, IReadOnlyList<PendingUserResult>>
{
    public async Task<IReadOnlyList<PendingUserResult>> Handle(GetPendingUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await userRepository.GetPendingUsersAsync(cancellationToken);
        return users
            .Select(u => new PendingUserResult(u.Id, u.Email, u.Role.ToString(), u.CreatedAt))
            .ToList();
    }
}
