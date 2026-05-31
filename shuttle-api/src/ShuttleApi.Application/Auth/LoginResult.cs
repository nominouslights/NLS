namespace ShuttleApi.Application.Auth;

public sealed record LoginResult(string AccessToken, string RefreshToken, string Role, bool MustChangePassword);
