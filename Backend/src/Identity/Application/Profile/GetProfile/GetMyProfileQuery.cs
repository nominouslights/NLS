using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Profile.GetProfile;

/// <summary>
/// Reads the caller's own account. <paramref name="UserId"/> is stamped by the endpoint from the
/// access token's <c>sub</c> claim and never bound from the route or body — that, plus the
/// tenant-scoped repository read, is what makes "you can only ever read yourself" a property of
/// the code rather than of the URL.
/// </summary>
public sealed record GetMyProfileQuery(Guid UserId) : IQuery<MyProfileResponse>;
