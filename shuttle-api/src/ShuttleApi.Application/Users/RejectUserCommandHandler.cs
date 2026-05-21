using MediatR;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Users;

namespace ShuttleApi.Application.Users;

internal sealed class RejectUserCommandHandler(IUserRepository userRepository)
    : IRequestHandler<RejectUserCommand>
{
    public async Task Handle(RejectUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException($"User {request.UserId} not found.");

        user.Deactivate();
        await userRepository.UpdateAsync(user, cancellationToken);
    }
}
