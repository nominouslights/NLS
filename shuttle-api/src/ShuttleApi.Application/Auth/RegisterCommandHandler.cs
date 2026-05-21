using MediatR;
using ShuttleApi.Application.Services;
using ShuttleApi.Domain.Common;
using ShuttleApi.Domain.Users;

namespace ShuttleApi.Application.Auth;

internal sealed class RegisterCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher)
    : IRequestHandler<RegisterCommand, RegisterResult>
{
    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var existing = await userRepository.GetByEmailAsync(request.Email.ToLowerInvariant(), cancellationToken);
        if (existing is not null)
            throw new ConflictException("Email already in use.");

        var passwordHash = passwordHasher.Hash(request.Password);
        var user = User.Create(request.Email, passwordHash, request.Role);

        await userRepository.AddAsync(user, cancellationToken);

        return new RegisterResult(user.Id, user.Email, user.Role.ToString());
    }
}
