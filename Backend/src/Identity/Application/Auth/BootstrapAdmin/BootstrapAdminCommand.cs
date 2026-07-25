using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.BootstrapAdmin;

/// <summary>
/// Mints a new Admin user given a valid, unconsumed bootstrap token. Anonymous by design —
/// gated by knowledge of the token itself, not by an existing session. Returns the new
/// user's id.
/// </summary>
public sealed record BootstrapAdminCommand(string Token, string Email, string Password) : ICommand<Guid>;
