using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Profile.UpdateProfile;

/// <summary>
/// No inline validation: trimming, length and the no-op rule all live in
/// <see cref="User.UpdateProfile"/>, so the aggregate stays the single place those rules are
/// stated. Returns the stored profile so one round trip updates every part of the screen with
/// exactly what was persisted, trimming included — <c>LoginCommand</c> is the precedent for a
/// command carrying a response.
/// </summary>
public sealed class UpdateMyProfileCommandHandler(IUserRepository userRepository)
    : ICommandHandler<UpdateMyProfileCommand, MyProfileResponse>
{
    public async Task<Result<MyProfileResponse>> Handle(
        UpdateMyProfileCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdForTenantAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<MyProfileResponse>(UserErrors.NotFound);
        }

        var updated = user.UpdateProfile(command.FullName, command.JobTitle);
        if (updated.IsFailure)
        {
            // Nothing was assigned, so there is nothing to unwind — and deliberately no save.
            return Result.Failure<MyProfileResponse>(updated.Error);
        }

        // Saves unconditionally, including when UpdateProfile was a no-op: with no change
        // tracked, SaveChanges writes nothing and the audit pipeline sees no modified aggregate.
        await userRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new MyProfileResponse(
            user.Id, user.Email, user.Role, user.FullName, user.JobTitle));
    }
}
