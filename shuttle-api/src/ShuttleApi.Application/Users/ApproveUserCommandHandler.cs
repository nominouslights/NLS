using MediatR;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Users;

namespace ShuttleApi.Application.Users;

internal sealed class ApproveUserCommandHandler(IUserRepository userRepository)
    : IRequestHandler<ApproveUserCommand>
{
    public async Task Handle(ApproveUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException($"User {request.UserId} not found.");

        user.Activate();
        await userRepository.UpdateAsync(user, cancellationToken);
    }
}
