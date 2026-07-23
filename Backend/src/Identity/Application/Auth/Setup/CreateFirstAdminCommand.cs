using NorthernLink.Identity.Application.Auth;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.Setup;

/// <summary>
/// First-run only: creates the initial Admin account and returns a token pair so the caller is
/// signed straight in. Succeeds solely while no user exists (see the handler's atomic guard).
/// </summary>
public sealed record CreateFirstAdminCommand(string Email, string Password) : ICommand<LoginResponse>;
