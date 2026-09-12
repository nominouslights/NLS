using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Profile.UpdateProfile;

/// <summary>
/// Writes the caller's own name and job title. Both fields are the <b>complete</b> new value, not
/// a patch: null means "cleared", never "leave as it was", which is why the endpoint is a PUT.
/// <para>
/// <paramref name="UserId"/> comes from the access token's <c>sub</c> claim, stamped by the
/// endpoint — never from the request body. A caller who could name the id could edit anyone.
/// </para>
/// </summary>
public sealed record UpdateMyProfileCommand(
    Guid UserId,
    string? FullName,
    string? JobTitle) : ICommand<MyProfileResponse>;
