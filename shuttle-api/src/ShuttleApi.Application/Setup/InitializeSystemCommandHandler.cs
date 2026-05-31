using ShuttleApi.Application.Common.Mediator;
using ShuttleApi.Application.Services;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Users;

namespace ShuttleApi.Application.Setup;

internal sealed class InitializeSystemCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher)
    : IRequestHandler<InitializeSystemCommand>
{
    public async Task Handle(InitializeSystemCommand request, CancellationToken cancellationToken)
    {
        var adminExists = await userRepository.AnyAdminExistsAsync(cancellationToken);
        if (adminExists)
            throw new ConflictException("System has already been initialized. An admin account exists.");

        var user = User.Create(request.Email, passwordHasher.Hash(request.Password), UserRole.Admin);
        user.Activate();
        user.RequirePasswordChange();

        await userRepository.AddAsync(user, cancellationToken);
    }
}
