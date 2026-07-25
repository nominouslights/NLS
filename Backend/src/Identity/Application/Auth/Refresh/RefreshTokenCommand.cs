using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.Refresh;

/// <summary>
/// Validates a presented refresh token and, if it's active (not revoked/expired), rotates
/// it: revokes the old one and issues a fresh access+refresh pair.
/// </summary>
public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<LoginResponse>;
