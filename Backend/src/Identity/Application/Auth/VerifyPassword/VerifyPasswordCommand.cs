using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.VerifyPassword;

/// <summary>
/// Step-up re-authentication (POST /api/identity/auth/verify-password): confirms the
/// already-signed-in caller still knows their own password before a sensitive act — today,
/// pairing trips that are operationally closed and possibly already billed.
/// <para>
/// <paramref name="UserId"/> is <b>never</b> taken from the request body. The endpoint reads it
/// from the access token's <c>sub</c> claim via <c>ICurrentActor</c>, so a caller can only ever
/// probe their own account; accepting an email or user id here would turn this into an
/// authenticated password oracle for every account on the tenant.
/// </para>
/// <para>
/// This mints nothing. No access token, no refresh token, no rotation — a successful check is a
/// bare 204, and the caller's existing session is exactly as it was.
/// </para>
/// </summary>
public sealed record VerifyPasswordCommand(Guid UserId, string Password) : ICommand;
