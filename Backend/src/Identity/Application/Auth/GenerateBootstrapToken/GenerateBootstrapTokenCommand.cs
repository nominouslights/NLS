using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Identity.Application.Auth.GenerateBootstrapToken;

/// <summary>
/// Mints a new bootstrap token for <paramref name="TenantId"/>, redeemable once to create a
/// user with <paramref name="Role"/>. Admin-only. Prior unconsumed tokens are left as-is
/// (simplicity over strict single-active-token enforcement — see the plan's own note on this).
/// </summary>
/// <param name="Role">
/// One of <see cref="Roles.Internal"/>. Fixed at mint time by the authenticated admin, because
/// the redeemer is anonymous and must not get to name their own privileges.
/// </param>
public sealed record GenerateBootstrapTokenCommand(Guid TenantId, string Role)
    : ICommand<GenerateBootstrapTokenResponse>;

/// <summary>
/// The raw bootstrap token — visible in plaintext exactly this once — and when it stops
/// being redeemable (<see cref="BootstrapTokenPolicy.Lifetime"/> after issuance).
/// </summary>
public sealed record GenerateBootstrapTokenResponse(string Token, DateTimeOffset ExpiresAtUtc);
