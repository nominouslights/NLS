namespace NorthernLink.Identity.Application.Auth;

/// <summary>Successful auth response for login and refresh — shared shape.</summary>
public sealed record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);
