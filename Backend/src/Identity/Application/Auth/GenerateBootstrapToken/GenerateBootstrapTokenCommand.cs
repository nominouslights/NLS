using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.GenerateBootstrapToken;

/// <summary>
/// Mints a new admin bootstrap token for <paramref name="TenantId"/>. Admin-only. Prior
/// unconsumed tokens are left as-is (simplicity over strict single-active-token enforcement —
/// see the plan's own note on this).
/// </summary>
public sealed record GenerateBootstrapTokenCommand(Guid TenantId) : ICommand<GenerateBootstrapTokenResponse>;

/// <summary>The raw bootstrap token — visible in plaintext exactly this once.</summary>
public sealed record GenerateBootstrapTokenResponse(string Token);
