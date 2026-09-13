namespace NorthernLink.Identity.Application.Profile;

/// <summary>
/// The signed-in caller's own account, as both profile slices return it.
/// <para>
/// <see cref="Email"/> and <see cref="Role"/> ride along even though neither is editable here,
/// so a profile screen renders its read-only and editable rows from one payload. The alternative
/// — reading those two off the access token — makes a screen specifically about account facts
/// depend on a snapshot that can be up to fifteen minutes stale.
/// </para>
/// </summary>
public sealed record MyProfileResponse(
    Guid UserId,
    string Email,
    string Role,
    string? FullName,
    string? JobTitle);
