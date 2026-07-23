namespace NorthernLink.Identity.Application.Auth;

/// <summary>
/// Admin bootstrap token lifetime policy. Deliberately short: a bootstrap token is an
/// invite handed from one admin to the next, not a credential to keep around — 15 minutes
/// covers "generate, copy, redeem" with margin and nothing more.
/// </summary>
internal static class BootstrapTokenPolicy
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
}
