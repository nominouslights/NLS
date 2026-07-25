namespace NorthernLink.Identity.Application.Auth;

/// <summary>Refresh token lifetime policy, shared by login and refresh handlers.</summary>
internal static class RefreshTokenPolicy
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);
}
