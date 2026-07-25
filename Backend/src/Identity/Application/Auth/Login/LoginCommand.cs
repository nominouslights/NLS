using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.Login;

/// <summary>Authenticates by email/password and issues a fresh access+refresh token pair.</summary>
public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResponse>;
