using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.Logout;

/// <summary>Revokes a refresh token. Idempotent — an unknown or already-revoked token still succeeds.</summary>
public sealed record LogoutCommand(string RefreshToken) : ICommand;
