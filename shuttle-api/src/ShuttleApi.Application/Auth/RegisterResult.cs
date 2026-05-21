namespace ShuttleApi.Application.Auth;

public sealed record RegisterResult(Guid UserId, string Email, string Role);
