using NorthernLink.Identity.Application.Abstractions;
using NorthernLink.Identity.Domain.Users;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Profile.GetProfile;

public sealed class GetMyProfileQueryHandler(IUserRepository userRepository)
    : IQueryHandler<GetMyProfileQuery, MyProfileResponse>
{
    public async Task<Result<MyProfileResponse>> Handle(
        GetMyProfileQuery query, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdForTenantAsync(query.UserId, cancellationToken);

        // A valid token naming a row that is gone, or belongs to another tenant, is a 404 —
        // not a null dereference and not a 401, since the caller did authenticate.
        return user is null
            ? Result.Failure<MyProfileResponse>(UserErrors.NotFound)
            : Result.Success(new MyProfileResponse(
                user.Id, user.Email, user.Role, user.FullName, user.JobTitle));
    }
}
